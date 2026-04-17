using System.Text.Json.Nodes;
using System.Text.Json;

namespace FabInspector.Core.Part
{
    public class Part
    {
        private readonly IFabricFileSystem _fileSystem;

            public Part? Parent { get; private set; }

        // The name of the file or folder
        public string FileSystemName { get; private set; }

        // The full path to the file or folder
        public string FileSystemPath { get; private set; }

        // The type of the part i.e. File or Folder
        public PartFileSystemTypeEnum PartFileSystemType { get; private set; }

        public JsonNode? JsonContent { get; set; }

    public Part(string fileSystemName, string fileSystemPath, Part? parent = null, PartFileSystemTypeEnum partType = default, IFabricFileSystem? fileSystem = null)
        {
            Parent = parent;
            FileSystemName = fileSystemName;
            FileSystemPath = fileSystemPath;
            PartFileSystemType = partType;
            _fileSystem = fileSystem ?? parent?._fileSystem ?? new FabricLocalFileSystem();
        }

        public List<Part>? Parts { get; set; }

        public IFabricFileSystem GetFileSystem()
        {
            return _fileSystem;
        }

        public static IEnumerable<Part> Flatten(Part part)
        {
            yield return part;

            if (part.Parts != null && part.Parts.Count > 0)
            {
                foreach (var p in part.Parts)
                {
                    foreach (var pp in Flatten(p))
                    {
                        yield return pp;
                    }
                }
            }
        }

        public void Save()
        {
            if (this.JsonContent != null)
            {
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonContent.ToJsonString(jsonOptions);
                _fileSystem.WriteAllText(FileSystemPath, updatedJson);
            }
        }
    }
}