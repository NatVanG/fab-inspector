using System;
using System.Collections.Generic;
using System.Linq;

namespace FabInspector.Core.Part
{
    internal class TMDLPartQuery : BasePartQuery
    {
        private const string DEFINITIONPBISM = "definition.pbism";
        private const string DEFINITIONFOLDER = "definition";
        private const string TMDLEXT = ".tmdl";
        private const string TABLESFOLDER = "tables";
        private const string CULTURESFOLDER = "cultures";
        private const string ROLESFOLDER = "roles";
        private const string PERSPECTIVESFOLDER = "perspectives";

        public TMDLPartQuery(string path) : this(path, null)
        {
        }

        public TMDLPartQuery(string path, IFabricFileSystem? fileSystem) : base(path, fileSystem)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            string semanticModelFolderPath;

            if (_fileSystem.DirectoryExists(path))
            {
                semanticModelFolderPath = path;
            }
            else if (_fileSystem.FileExists(path) && _fileSystem.GetFileName(path).Equals(DEFINITIONPBISM, StringComparison.OrdinalIgnoreCase))
            {
                semanticModelFolderPath = _fileSystem.GetDirectoryName(path);
            }
            else
            {
                throw new ArgumentException($"{path} does not exist.");
            }

            this.RootPart = new Part("root", semanticModelFolderPath, null!, PartFileSystemTypeEnum.Folder, _fileSystem);
            SetParts(this.RootPart);
        }

        private static bool IsNamedFile(Part part, string fileName)
        {
            return part.PartFileSystemType == PartFileSystemTypeEnum.File
                && part.FileSystemName.Equals(fileName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTmdlInFolder(Part part, string folderName)
        {
            return part.PartFileSystemType == PartFileSystemTypeEnum.File
                && part.FileSystemName.EndsWith(TMDLEXT, StringComparison.OrdinalIgnoreCase)
                && part.Parent != null
                && part.Parent.FileSystemName.Equals(folderName, StringComparison.OrdinalIgnoreCase);
        }

        #region Methods invokeable from rules
        public Part? Definition(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, DEFINITIONPBISM)
                                  select p;

            return q.SingleOrDefault();
        }

        public Part? Database(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, "database.tmdl")
                                  && p.Parent != null
                                  && p.Parent.FileSystemName.Equals(DEFINITIONFOLDER, StringComparison.OrdinalIgnoreCase)
                                  select p;

            return q.SingleOrDefault();
        }

        public Part? Expressions(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, "expressions.tmdl")
                                  && p.Parent != null
                                  && p.Parent.FileSystemName.Equals(DEFINITIONFOLDER, StringComparison.OrdinalIgnoreCase)
                                  select p;

            return q.SingleOrDefault();
        }

        public Part? Model(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, "model.tmdl")
                                  && p.Parent != null
                                  && p.Parent.FileSystemName.Equals(DEFINITIONFOLDER, StringComparison.OrdinalIgnoreCase)
                                  select p;

            return q.SingleOrDefault();
        }

        public Part? Relationships(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, "relationships.tmdl")
                                  && p.Parent != null
                                  && p.Parent.FileSystemName.Equals(DEFINITIONFOLDER, StringComparison.OrdinalIgnoreCase)
                                  select p;

            return q.SingleOrDefault();
        }

        public Part? DataSources(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, "dataSources.tmdl")
                                  && p.Parent != null
                                  && p.Parent.FileSystemName.Equals(DEFINITIONFOLDER, StringComparison.OrdinalIgnoreCase)
                                  select p;

            return q.SingleOrDefault();
        }

        public Part? Functions(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where IsNamedFile(p, "functions.tmdl")
                                  && p.Parent != null
                                  && p.Parent.FileSystemName.Equals(DEFINITIONFOLDER, StringComparison.OrdinalIgnoreCase)
                                  select p;

            return q.SingleOrDefault();
        }

        public List<Part> Tables(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                  where IsTmdlInFolder(p, TABLESFOLDER)
                                  select p;

            return q.ToList();
        }

        public List<Part> Cultures(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                  where IsTmdlInFolder(p, CULTURESFOLDER)
                                  select p;

            return q.ToList();
        }

        public List<Part> Roles(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                  where IsTmdlInFolder(p, ROLESFOLDER)
                                  select p;

            return q.ToList();
        }

        public List<Part> Perspectives(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                  where IsTmdlInFolder(p, PERSPECTIVESFOLDER)
                                  select p;

            return q.ToList();
        }
        #endregion
    }
}
