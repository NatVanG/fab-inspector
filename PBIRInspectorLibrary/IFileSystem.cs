using System;
using System.Collections.Generic;
using System.IO;

namespace PBIRInspectorLibrary
{
    /// <summary>
    /// Abstraction for file system operations to enable in-memory and physical file system support
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// Gets the root path for this file system instance
        /// </summary>
        string RootPath { get; }

        /// <summary>
        /// Checks if a file exists at the specified path
        /// </summary>
        bool FileExists(string path);

        /// <summary>
        /// Checks if a directory exists at the specified path
        /// </summary>
        bool DirectoryExists(string path);

        /// <summary>
        /// Reads all bytes from a file
        /// </summary>
        byte[] ReadAllBytes(string path);

        /// <summary>
        /// Reads all text from a file
        /// </summary>
        string ReadAllText(string path);

        /// <summary>
        /// Writes text to a file
        /// </summary>
        void WriteAllText(string path, string contents);

        /// <summary>
        /// Gets all files in a directory matching the search pattern
        /// </summary>
        IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption);

        /// <summary>
        /// Gets all files in a directory
        /// </summary>
        IEnumerable<string> GetFiles(string path);

        /// <summary>
        /// Gets all directories in a directory
        /// </summary>
        IEnumerable<string> GetDirectories(string path);

        /// <summary>
        /// Gets the file name from a path
        /// </summary>
        string GetFileName(string path);

        /// <summary>
        /// Gets the directory name from a path
        /// </summary>
        string GetDirectoryName(string path);

        /// <summary>
        /// Gets the file extension from a path
        /// </summary>
        string GetExtension(string path);

        /// <summary>
        /// Combines path segments
        /// </summary>
        string PathCombine(params string[] paths);

        /// <summary>
        /// Gets the size of a file in bytes
        /// </summary>
        long GetFileSize(string path);

        /// <summary>
        /// Gets the file name without extension from a path
        /// </summary>
        string GetFileNameWithoutExtension(string path);
    }
}
