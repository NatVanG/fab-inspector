using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json.Nodes;
using System.ComponentModel;
using PBIRInspectorLibrary.Part;
using System.Reflection;
using System.Runtime.CompilerServices;
using Json.More;
using System.Text.RegularExpressions;

namespace PBIRInspectorLibrary.Part
{
    /// <summary>
    /// https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report#pbir-format
    /// </summary>
    internal class PBIRPartQuery : BasePartQuery
    {
        //TODO: harden logic to extract path value here.
        private const string REPORTFOLDERPOINTER = "/artifacts/0/report/path";
        private const string PBIPEXT = ".pbip";
        private const string PBIXEXT = ".pbix";

        public PBIRPartQuery(string path) : this(path, null)
        {
        }

        public PBIRPartQuery(string path, IFabricFileSystem? fileSystem) : base(path, fileSystem)
        {
            if (path == null || path.Length == 0) throw new ArgumentNullException(nameof(path));
            if (!_fileSystem.FileExists(path) && path.EndsWith(PBIPEXT)) throw new ArgumentException($"PBI Desktop file {path} does not exist.");
            if (path.ToLower().EndsWith(PBIXEXT)) throw new ArgumentException($"PBIX files are not currently supported, please specify a PBIP or a Fabric items directory path.");
            if (_fileSystem.FileExists(path) && !path.ToLower().EndsWith(PBIPEXT)) throw new ArgumentException($"PBI Desktop file {path} must have .pbip extension.");
            if (!_fileSystem.FileExists(path) && !_fileSystem.DirectoryExists(path)) throw new ArgumentException($"{path} does not exist.");

            string? reportFolderPath = null;

            if (_fileSystem.FileExists(path) && path.EndsWith(PBIPEXT))
            {
                var pbip = new Part("pbip", path, null!, PartFileSystemTypeEnum.File, _fileSystem);
                reportFolderPath = ReportPath(pbip);
            }
            else if (_fileSystem.DirectoryExists(path))
            {
                reportFolderPath = path;
            }

            this.RootPart = new Part("root", reportFolderPath!, null!, PartFileSystemTypeEnum.Folder, _fileSystem);
            SetParts(this.RootPart);
        }

        //TODO: add support for pbir or folder.
        private string ReportPath(Part context)
        {
            var node = PartUtils.ToJsonNode(context);
            var val = node != null ? PartUtils.TryGetJsonNodeStringValue(node, REPORTFOLDERPOINTER) : null;

            val = _fileSystem.PathCombine(_fileSystem.GetDirectoryName(context.FileSystemPath), val ?? string.Empty);

            return val;
        }

        #region Methods invokeable from rules
        public Part Report(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                          where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("report.json")
                                          select p;

            return q.Single();
        }


        public Part? ReportExtensions(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                  where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("reportExtensions.json")
                                  select p;

            return q.SingleOrDefault();
        }

        public Part Version(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                          where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("version.json")
                                          select p;

            return q.Single();
        }

        public Part PagesHeader(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                          where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("pages.json")
                                          select p;

            return q.Single();
        }

        public List<Part> Pages(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                          where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("page.json")
                                          select p;

            return q.ToList();
        }

        public List<Part> AllVisuals(Part context)
        {
            return Visuals(TopParent(context));
        }

        public List<Part> Visuals(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                            where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("visual.json")
                                            select p;

            return q.ToList();
        }

        public List<Part> AllMobileVisuals(Part context)
        {
            return MobileVisuals(TopParent(context));
        }

        public List<Part> MobileVisuals(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                            where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("mobile.json")
                                            select p;

            return q.ToList();
        }

        public Part? BookmarksHeader(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(TopParent(context))
                                            where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("bookmarks.json")
                                            select p;

            return q.SingleOrDefault();
        }


        public List<Part> AllBookmarks(Part context)
        {
            return Bookmarks(TopParent(context));
        }

        public List<Part> Bookmarks(Part context)
        {
            IEnumerable<Part> q = from p in Part.Flatten(context.PartFileSystemType == PartFileSystemTypeEnum.File ? context.Parent ?? context : context)
                                            where p.PartFileSystemType == PartFileSystemTypeEnum.File && p.FileSystemName.EndsWith("bookmark.json")
                                            select p;

            return q.ToList();
        }
        #endregion

    }
}