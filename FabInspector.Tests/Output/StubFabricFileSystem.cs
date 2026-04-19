using FabInspector.Core;

namespace FabInspector.Tests.Output
{
    /// <summary>
    /// Minimal stub for IFabricFileSystem used in output writer tests.
    /// </summary>
    internal class StubFabricFileSystem : IFabricFileSystem
    {
        public string RootPath => string.Empty;
        public IEnumerable<string>? ScopedItemTypes { get; set; }
        public string GetRelativePath(string fullPath) => fullPath;
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public byte[] ReadAllBytes(string path) => Array.Empty<byte>();
        public string ReadAllText(string path) => string.Empty;
        public void WriteAllText(string path, string contents) { }
        public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption) => Array.Empty<string>();
        public IEnumerable<string> GetFiles(string path) => Array.Empty<string>();
        public IEnumerable<FabricItem> GetFabricItems(string path) => Array.Empty<FabricItem>();
        public IEnumerable<string> GetDirectories(string path) => Array.Empty<string>();
        public string GetFileName(string path) => Path.GetFileName(path);
        public string GetDirectoryName(string path) => Path.GetDirectoryName(path) ?? string.Empty;
        public string GetExtension(string path) => Path.GetExtension(path);
        public string PathCombine(params string[] paths) => Path.Combine(paths);
        public long GetFileSize(string path) => 0;
        public string GetFileNameWithoutExtension(string path) => Path.GetFileNameWithoutExtension(path);
    }
}
