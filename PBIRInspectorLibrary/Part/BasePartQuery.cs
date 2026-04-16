using Json.Pointer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PBIRInspectorLibrary.Part
{
    internal class BasePartQuery : IPartQuery
    {
        private const string UNIQUEPARTMETHODNAME = "UniquePart";
        private const string NAMEPOINTER = "/name";
        private const string DISPLAYNAMEPOINTER = "/displayName";

        protected readonly IFabricFileSystem _fileSystem;

        public BasePartQuery(string fileSystemPath) : this(fileSystemPath, null)
        {
        }

        public BasePartQuery(string fileSystemPath, IFabricFileSystem? fileSystem)
        {
            _fileSystem = fileSystem ?? new FabricLocalFileSystem();
        }

        public Part RootPart { get; set; } = null!;

        public virtual object? Invoke(string query, Part context)
        {
            object? result = null;
            if (string.IsNullOrEmpty(query)) return context;

            var type = this.GetType();
            System.Reflection.MethodInfo? mi = type.GetMethod(query);
            if (mi != null)
            {
                //TODO: retrict invokable methods to their own namespace?
                result = mi.Invoke(this, new object?[] { context });
            }
            else
            {
                result = SearchParts(query, context);
            }

            return result;
        }

        private protected virtual object? SearchParts(string query, Part context)
        {
            //Normalizing FileSystemPath to easily regex match file system paths across different OS. e.g. folder1\/.*\/copyjob-content\.json$ 
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where Regex.IsMatch(Utils.NormalizePath(p.FileSystemPath), query, RegexOptions.IgnoreCase)
                                  select p;

            if (q is null) return null;
            
            return q.ToList();
        }

        private protected void SetParts()
        {
            if (this.RootPart == null) throw new ArgumentNullException("RootPart is not set.");
            SetParts(this.RootPart);
        }

        private protected void SetParts(Part context)
        {
            if (this.RootPart == null)
            {
                this.RootPart = context;
            }
            if (!_fileSystem.DirectoryExists(context.FileSystemPath)) return;

            context.Parts = new List<Part>();

            foreach (string filePath in _fileSystem.GetFiles(context.FileSystemPath))
            {
                var fileName = _fileSystem.GetFileName(filePath);
                Part filePart = new Part(fileName, filePath, context, PartFileSystemTypeEnum.File, _fileSystem);
                context.Parts.Add(filePart);
            }

            foreach (string dirPath in _fileSystem.GetDirectories(context.FileSystemPath))
            {
                var dirName = _fileSystem.GetFileName(dirPath);
                Part dirPart = new Part(dirName, dirPath, context, PartFileSystemTypeEnum.Folder, _fileSystem);
                context.Parts.Add(dirPart);
                SetParts(dirPart);
            }
        }



        #region Methods invokeable from rules 
        public Part? Parent(Part context)
        {
            return context.Parent;
        }

        public Part TopParent(Part context)
        {
            if (context.Parent == null) return context;
            return TopParent(context.Parent);
        }

        public Part Platform(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith(".platform")
                                  select p;

            return q.Single();
        }

        //TODO: implement for folders
        public string PartName(Part context)
        {
            string? val = null;

            if (context.PartFileSystemType == PartFileSystemTypeEnum.File && context.FileSystemName.EndsWith(".json"))
            {
                var node = PartUtils.ToJsonNode(context);
                    val = node != null ? PartUtils.TryGetJsonNodeStringValue(node, NAMEPOINTER) : null;
            }

            return val ?? context.FileSystemName;
        }

        //TODO: implement for folders
        public string PartDisplayName(Part context)
        {
            string? val = null;

            if (context.PartFileSystemType == PartFileSystemTypeEnum.File && context.FileSystemName.EndsWith(".json"))
            {
                var node = PartUtils.ToJsonNode(context);
                    val = node != null ? PartUtils.TryGetJsonNodeStringValue(node, DISPLAYNAMEPOINTER) : null;
            }

            return val ?? context.FileSystemName;
        }

        public string PartFileSystemName(Part context)
        {
            return context.FileSystemName;
        }

        public string PartFileSystemPath(Part context)
        {
            return context.FileSystemPath;
        }

        public string? PartFileExtension(Part context)
        {
            if (context.PartFileSystemType != PartFileSystemTypeEnum.File)
            {
                return null;
            }
            return Path.GetExtension(context.FileSystemName);
        }

        public string PartFileSystemType(Part context)
        {
            return context.PartFileSystemType.ToString();
        }

        public List<Part> Files(Part context)
        {
              IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                  where p.PartFileSystemType == PartFileSystemTypeEnum.File
                                  select p;

            return q.ToList();
        }
        #endregion
    }
}