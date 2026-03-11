using PBIRInspectorLibrary.Exceptions;
using PBIRInspectorLibrary.Part;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PBIRInspectorLibrary
{
    public class FabricLocalFileSystem : LocalFileSystem, IFabricFileSystem
    {

        /// <summary>
        /// Initializes a new instance of PhysicalFileSystem with an empty root path
        /// </summary>
        public FabricLocalFileSystem() : base(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of PhysicalFileSystem with a specified root path
        /// </summary>
        /// <param name="rootPath">The root path for this file system instance</param>
        public FabricLocalFileSystem(string rootPath) : base(rootPath)
        {

        }

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
                    JsonNode? platformNode = JsonNode.Parse(this.ReadAllBytes(platformFile));
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
                        DisplayName = PartUtils.TryGetJsonNodeStringValue(platformNode, "/metadata/displayName") ?? System.IO.Path.GetFileNameWithoutExtension(platformFile),
                        Type = itemType,
                        Description = "",
                        WorkspaceId = "",
                        FilePath = platformFile,
                        DirectoryPath = System.IO.Path.GetDirectoryName(platformFile) ?? string.Empty
                    };
                    fabricItems.Add(fabricItem);
                }
            }
            return fabricItems;
        }

        public string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(this.RootPath)) return fullPath;
            return fullPath.Substring(fullPath.IndexOf(this.RootPath) + this.RootPath.Length);
        }
    }
}
