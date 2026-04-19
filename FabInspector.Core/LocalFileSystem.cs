using System;
using System.Collections.Generic;
using System.IO;

namespace FabInspector.Core
{
    /// <summary>
    /// Physical file system implementation that uses actual file system operations.
    /// This class is internal; use <see cref="FabricLocalFileSystem"/> instead.
    /// </summary>
    internal class LocalFileSystem
    {
        private readonly string _rootPath;

        /// <summary>
        /// Initializes a new instance of PhysicalFileSystem with an empty root path
        /// </summary>
        public LocalFileSystem() : this(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of PhysicalFileSystem with a specified root path
        /// </summary>
        /// <param name="rootPath">The root path for this file system instance</param>
        public LocalFileSystem(string rootPath)
        {
            _rootPath = rootPath ?? string.Empty;
        }

        /// <summary>
        /// Gets the root path for this file system instance
        /// </summary>
        public string RootPath => _rootPath;

        public IEnumerable<string>? ScopedItemTypes { get; set; }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public byte[] ReadAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.GetFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return Directory.GetFiles(path);
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return Directory.GetDirectories(path);
        }

        public string GetFileName(string path)
        {
            return Path.GetFileName(path);
        }

        public string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }

        public string GetExtension(string path)
        {
            return Path.GetExtension(path);
        }

        public string PathCombine(params string[] paths)
        {
            return Path.Combine(paths);
        }

        public long GetFileSize(string path)
        {
            var fileInfo = new FileInfo(path);
            return fileInfo.Length;
        }

        public string GetFileNameWithoutExtension(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }
    }
}
