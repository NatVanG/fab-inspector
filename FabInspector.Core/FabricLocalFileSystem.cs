using FabInspector.Core.Exceptions;
using FabInspector.Core.Part;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FabInspector.Core
{
    public sealed class FabricLocalFileSystem : IFabricFileSystem
    {
        private readonly string _rootPath;
        private readonly string _rootDir;

        public FabricLocalFileSystem() : this(string.Empty)
        {
        }

        public FabricLocalFileSystem(string rootPath)
        {
            _rootPath = rootPath ?? string.Empty;
            _rootDir = File.Exists(rootPath) ? Path.GetDirectoryName(rootPath) ?? string.Empty : _rootPath;
        }

        public string RootPath => _rootPath;

        public IEnumerable<string>? ScopedItemTypes { get; set; }

        public bool FileExists(string path) => File.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(path);

        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        public string ReadAllText(string path) => File.ReadAllText(path);

        public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

        public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
            => Directory.GetFiles(path, searchPattern, searchOption);

        public IEnumerable<string> GetFiles(string path) => Directory.GetFiles(path);

        public IEnumerable<string> GetDirectories(string path) => Directory.GetDirectories(path);

        public string GetFileName(string path) => Path.GetFileName(path);

        public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;

        public string GetExtension(string path) => Path.GetExtension(path);

        public string PathCombine(params string[] paths) => Path.Combine(paths);

        public long GetFileSize(string path) => new FileInfo(path).Length;

        public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);

        public IEnumerable<FabricItem> GetFabricItems(string path)
        {
            List<FabricItem> fabricItems = new List<FabricItem>();
            var platformFiles = this
                    .GetFiles(path, "*.platform", SearchOption.AllDirectories)
                    .ToList();

            if (platformFiles != null && platformFiles.Count != 0)
            {
                foreach (var platformFile in platformFiles)
                {
                    JsonNode? platformNode = JsonNode.Parse(this.ReadAllText(platformFile));
                    if (platformNode == null)
                    {
                        //TODO: raise error but continue
                        //throw new PBIRInspectorException(string.Format("Could not parse platform file at \"{0}\".", platformFile));
                        continue;
                    }

                    var itemType = PartUtils.TryGetJsonNodeStringValue(platformNode, "/metadata/type")!.ToLowerInvariant();
                    var fabricItem = new FabricItem
                    {
                        Id = Guid.NewGuid().ToString(), //TODO: set to null/empty string?
                        DisplayName = PartUtils.TryGetJsonNodeStringValue(platformNode, "/metadata/displayName") ?? Path.GetFileNameWithoutExtension(platformFile),
                        Type = itemType,
                        Description = "",
                        WorkspaceId = "",
                        FilePath = platformFile,
                        DirectoryPath = Path.GetDirectoryName(platformFile) ?? string.Empty
                    };
                    fabricItems.Add(fabricItem);
                }
            }
            return fabricItems.Where(_ => this.ScopedItemTypes == null
            || this.ScopedItemTypes.Contains("*", StringComparer.OrdinalIgnoreCase)
            || this.ScopedItemTypes.Contains(_.Type, StringComparer.OrdinalIgnoreCase)
            || (_.Type.Equals("Report", StringComparison.InvariantCultureIgnoreCase) && this.ScopedItemTypes.Contains("report_deprecated", StringComparer.OrdinalIgnoreCase)));
        }

        public string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(this._rootDir)) return fullPath;
            return fullPath.Substring(fullPath.IndexOf(this._rootDir) + this._rootDir.Length);
        }
    }
}
