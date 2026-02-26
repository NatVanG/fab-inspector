using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PBIRInspectorLibrary
{
    /// <summary>
    /// In-memory file system implementation for testing and scenarios where physical files don't exist
    /// </summary>
    public class InMemoryFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _rootPath;

        public InMemoryFileSystem() : this(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of InMemoryFileSystem with a specified root path
        /// </summary>
        /// <param name="rootPath">The root path for this file system instance</param>
        public InMemoryFileSystem(string rootPath)
        {
            _rootPath = rootPath ?? string.Empty;
            
            // Root directory always exists
            _directories.Add("");
            _directories.Add("/");
            _directories.Add("\\");
        }

        /// <summary>
        /// Gets the root path for this file system instance
        /// </summary>
        public string RootPath => _rootPath;

        /// <summary>
        /// Adds a file to the in-memory file system
        /// </summary>
        public void AddFile(string path, string contents)
        {
            var normalizedPath = NormalizePath(path);
            _files[normalizedPath] = contents;
            
            // Ensure parent directories exist
            var directory = GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                AddDirectory(directory);
            }
        }

        /// <summary>
        /// Adds a directory to the in-memory file system
        /// </summary>
        public void AddDirectory(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (!_directories.Contains(normalizedPath))
            {
                _directories.Add(normalizedPath);
                
                // Ensure parent directories exist
                var parent = GetDirectoryName(normalizedPath);
                if (!string.IsNullOrEmpty(parent) && parent != normalizedPath)
                {
                    AddDirectory(parent);
                }
            }
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(NormalizePath(path));
        }

        public bool DirectoryExists(string path)
        {
            return _directories.Contains(NormalizePath(path));
        }

        public byte[] ReadAllBytes(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (!_files.TryGetValue(normalizedPath, out var contents))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return Encoding.UTF8.GetBytes(contents);
        }

        public string ReadAllText(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (!_files.TryGetValue(normalizedPath, out var contents))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return contents;
        }

        public void WriteAllText(string path, string contents)
        {
            var normalizedPath = NormalizePath(path);
            _files[normalizedPath] = contents;
            
            // Ensure parent directory exists
            var directory = GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                AddDirectory(directory);
            }
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var normalizedPath = NormalizePath(path);
            var files = _files.Keys.Where(f =>
            {
                var dir = GetDirectoryName(f);
                if (searchOption == SearchOption.AllDirectories)
                {
                    return dir != null && (dir == normalizedPath || dir.StartsWith(normalizedPath + Path.DirectorySeparatorChar) || dir.StartsWith(normalizedPath + Path.AltDirectorySeparatorChar));
                }
                else
                {
                    return dir == normalizedPath;
                }
            });

            // Apply search pattern
            if (searchPattern != null && searchPattern != "*")
            {
                var pattern = ConvertWildcardToRegex(searchPattern);
                files = files.Where(f => System.Text.RegularExpressions.Regex.IsMatch(GetFileName(f), pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
            }

            return files.ToList();
        }

        public IEnumerable<string> GetFiles(string path)
        {
            var normalizedPath = NormalizePath(path);
            return _files.Keys.Where(f => GetDirectoryName(f) == normalizedPath).ToList();
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            var normalizedPath = NormalizePath(path);
            return _directories.Where(d =>
            {
                if (d == normalizedPath) return false;
                var parent = GetDirectoryName(d);
                return parent == normalizedPath;
            }).ToList();
        }

        public string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var normalizedPath = NormalizePath(path);
            var lastSeparator = Math.Max(
                normalizedPath.LastIndexOf(Path.DirectorySeparatorChar),
                normalizedPath.LastIndexOf(Path.AltDirectorySeparatorChar)
            );
            return lastSeparator >= 0 ? normalizedPath.Substring(lastSeparator + 1) : normalizedPath;
        }

        public string GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var normalizedPath = NormalizePath(path);
            var lastSeparator = Math.Max(
                normalizedPath.LastIndexOf(Path.DirectorySeparatorChar),
                normalizedPath.LastIndexOf(Path.AltDirectorySeparatorChar)
            );
            return lastSeparator >= 0 ? normalizedPath.Substring(0, lastSeparator) : "";
        }

        public string GetExtension(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var fileName = GetFileName(path);
            var lastDot = fileName.LastIndexOf('.');
            return lastDot >= 0 ? fileName.Substring(lastDot) : "";
        }

        public string PathCombine(params string[] paths)
        {
            if (paths == null || paths.Length == 0) return "";
            var result = paths[0];
            for (int i = 1; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i])) continue;
                result = result.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar
                    + paths[i].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            return NormalizePath(result);
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            
            // Replace alternate directory separator with primary
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            
            // Remove trailing separators for directories (but not root)
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            
            if (string.IsNullOrEmpty(path))
            {
                return Path.DirectorySeparatorChar.ToString();
            }
            
            return path;
        }

        private string ConvertWildcardToRegex(string pattern)
        {
            return "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
        }

        public long GetFileSize(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (!_files.TryGetValue(normalizedPath, out var contents))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }
            return Encoding.UTF8.GetByteCount(contents);
        }

        public string GetFileNameWithoutExtension(string path)
        {
            var fileName = GetFileName(path);
            var extension = GetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return fileName;
            return fileName.Substring(0, fileName.Length - extension.Length);
        }
    }
}
