using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;

namespace PBIRInspectorLibrary
{
    /// <summary>
    /// File system implementation that uses Microsoft Fabric REST API to retrieve workspace items on demand.
    /// This implementation provides lazy loading of items and their definitions, only downloading base64 payloads when needed.
    /// Supports two instantiation modes:
    /// 1. Workspace-scoped: Access all items in a workspace
    /// 2. Item-scoped: Access only a specific item within a workspace
    /// </summary>
    /// <remarks>
    /// Virtual path structure: /{itemName}/{partPath}
    /// Examples:
    /// - /MyReport/definition.pbir
    /// - /MyReport/report.json
    /// 
    /// Usage:
    /// // Workspace-scoped (access all items)
    /// var credential = new DefaultAzureCredential();
    /// var fs = new FabricFileSystem(workspaceId, credential);
    /// 
    /// // Item-scoped (access single item)
    /// var fs = new FabricFileSystem(workspaceId, itemId, credential);
    /// </remarks>
    public class FabricFileSystem : IFileSystem
    {
        private readonly string _workspaceId;
        private readonly TokenCredential _credential;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api.fabric.microsoft.com/v1";
        
        // Scope tracking fields (null for workspace-scoped, set for item-scoped)
        private readonly string? _scopedItemId;
        private string? _scopedItemName;
        
        // Cache for workspace items (lazy loaded once)
        private List<FabricItem>? _workspaceItems;
        private readonly object _itemsLock = new object();
        
        // Cache for item definitions (lazy loaded per item)
        private readonly Dictionary<string, FabricItemDefinition> _itemDefinitions = new Dictionary<string, FabricItemDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _definitionLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the root path for this file system instance
        /// </summary>
        public string RootPath => _scopedItemId != null && _scopedItemName != null 
            ? $"/{_scopedItemName}" 
            : "/";

        /// <summary>
        /// Initializes a new instance of the FabricFileSystem class for workspace-scoped access
        /// </summary>
        /// <param name="workspaceId">The Fabric workspace ID (GUID)</param>
        /// <param name="credential">Azure credential for authentication with automatic token refresh</param>
        /// <param name="httpClient">Optional HttpClient instance for testing/reuse</param>
        public FabricFileSystem(string workspaceId, TokenCredential credential, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                throw new ArgumentNullException(nameof(workspaceId));
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));

            _workspaceId = workspaceId;
            _credential = credential;
            _httpClient = httpClient ?? new HttpClient();
            _scopedItemId = null;
            _scopedItemName = null;
            
            // Configure default headers
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Initializes a new instance of the FabricFileSystem class for item-scoped access
        /// </summary>
        /// <param name="workspaceId">The Fabric workspace ID (GUID)</param>
        /// <param name="itemId">The Fabric item ID (GUID) to scope access to</param>
        /// <param name="credential">Azure credential for authentication with automatic token refresh</param>
        /// <param name="httpClient">Optional HttpClient instance for testing/reuse</param>
        public FabricFileSystem(string workspaceId, string itemId, TokenCredential credential, HttpClient? httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                throw new ArgumentNullException(nameof(workspaceId));
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentNullException(nameof(itemId));
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));

            _workspaceId = workspaceId;
            _credential = credential;
            _httpClient = httpClient ?? new HttpClient();
            _scopedItemId = itemId;
            
            // Configure default headers
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            // Eagerly load item metadata to validate itemId and cache item name
            var item = LoadItemMetadataAsync(itemId).GetAwaiter().GetResult();
            _scopedItemName = item.DisplayName;
        }

        #region Private Helper Methods

        /// <summary>
        /// Gets an access token from the credential and configures HTTP client authorization
        /// </summary>
        private async Task EnsureAuthenticatedAsync()
        {
            var tokenRequestContext = new TokenRequestContext(new[] { "https://api.fabric.microsoft.com/.default" });
            var token = await _credential.GetTokenAsync(tokenRequestContext, default);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        }

        /// <summary>
        /// Ensures workspace items are loaded (lazy loading)
        /// </summary>
        private void EnsureWorkspaceItemsLoaded()
        {
            if (_workspaceItems != null)
                return;

            lock (_itemsLock)
            {
                if (_workspaceItems != null)
                    return;

                // If scoped to a single item, create synthetic single-item list
                if (_scopedItemId != null)
                {
                    _workspaceItems = new List<FabricItem>
                    {
                        new FabricItem
                        {
                            Id = _scopedItemId,
                            DisplayName = _scopedItemName ?? string.Empty,
                            WorkspaceId = _workspaceId
                        }
                    };
                }
                else
                {
                    _workspaceItems = LoadWorkspaceItemsAsync().GetAwaiter().GetResult();
                }
            }
        }

        /// <summary>
        /// Loads a specific item's metadata from Fabric REST API
        /// </summary>
        private async Task<FabricItem> LoadItemMetadataAsync(string itemId)
        {
            await EnsureAuthenticatedAsync();
            
            var url = $"{_baseUrl}/workspaces/{_workspaceId}/items/{itemId}";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to load item metadata for {itemId}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FabricItem>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (result == null)
            {
                throw new HttpRequestException($"Failed to deserialize item metadata for {itemId}");
            }

            return result;
        }

        /// <summary>
        /// Loads all items in the workspace from Fabric REST API
        /// </summary>
        private async Task<List<FabricItem>> LoadWorkspaceItemsAsync()
        {
            await EnsureAuthenticatedAsync();
            
            var url = $"{_baseUrl}/workspaces/{_workspaceId}/items";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to load workspace items: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FabricItemsResponse>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return result?.Value ?? new List<FabricItem>();
        }

        /// <summary>
        /// Ensures a specific item's definition is loaded (lazy loading per item)
        /// </summary>
        private void EnsureItemDefinitionLoaded(string itemId)
        {
            if (_itemDefinitions.ContainsKey(itemId))
                return;

            // Get or create lock for this specific item
            object itemLock;
            lock (_definitionLocks)
            {
                if (!_definitionLocks.ContainsKey(itemId))
                {
                    _definitionLocks[itemId] = new object();
                }
                itemLock = _definitionLocks[itemId];
            }

            lock (itemLock)
            {
                if (_itemDefinitions.ContainsKey(itemId))
                    return;

                var definition = LoadItemDefinitionAsync(itemId).GetAwaiter().GetResult();
                _itemDefinitions[itemId] = definition;
            }
        }

        /// <summary>
        /// Loads a specific item's definition from Fabric REST API
        /// </summary>
        private async Task<FabricItemDefinition> LoadItemDefinitionAsync(string itemId)
        {
            await EnsureAuthenticatedAsync();
            
            var url = $"{_baseUrl}/workspaces/{_workspaceId}/items/{itemId}/getDefinition";
            var response = await _httpClient.PostAsync(url, null);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to load item definition for {itemId}: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FabricItemDefinitionResponse>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return result?.Definition ?? new FabricItemDefinition { Parts = new List<FabricItemPart>() };
        }

        /// <summary>
        /// Parses a virtual path into item name and part path
        /// </summary>
        private (string itemName, string partPath) ParsePath(string path)
        {
            var normalizedPath = NormalizePath(path);
            var segments = normalizedPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            
            if (segments.Length == 0)
                return (string.Empty, string.Empty);
            
            if (segments.Length == 1)
                return (segments[0], string.Empty);
            
            return (segments[0], string.Join(Path.DirectorySeparatorChar.ToString(), segments.Skip(1)));
        }

        /// <summary>
        /// Finds an item by name (case-insensitive)
        /// </summary>
        private FabricItem? FindItem(string itemName)
        {
            EnsureWorkspaceItemsLoaded();
            
            // If scoped to a single item, only return it if the name matches
            if (_scopedItemId != null && !string.Equals(_scopedItemName, itemName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            
            return _workspaceItems!.FirstOrDefault(i => string.Equals(i.DisplayName, itemName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds a part within an item's definition
        /// </summary>
        private FabricItemPart? FindPart(string itemId, string partPath)
        {
            EnsureItemDefinitionLoaded(itemId);
            var definition = _itemDefinitions[itemId];
            return definition.Parts?.FirstOrDefault(p => string.Equals(p.Path, partPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Normalizes a path by replacing alternate separators and removing trailing separators
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            path = path.TrimStart(Path.DirectorySeparatorChar);
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            
            return path;
        }

        /// <summary>
        /// Decodes a base64 string to UTF-8 text
        /// </summary>
        private string DecodeBase64(string base64)
        {
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        #endregion

        #region IFileSystem Implementation

        public bool FileExists(string path)
        {
            try
            {
                var (itemName, partPath) = ParsePath(path);
                
                if (string.IsNullOrEmpty(itemName))
                    return false;

                var item = FindItem(itemName);
                if (item == null)
                    return false;

                // If no part path, this is a directory (item), not a file
                if (string.IsNullOrEmpty(partPath))
                    return false;

                var part = FindPart(item.Id, partPath);
                return part != null;
            }
            catch
            {
                return false;
            }
        }

        public bool DirectoryExists(string path)
        {
            try
            {
                var (itemName, partPath) = ParsePath(path);
                
                // Root directory always exists
                if (string.IsNullOrEmpty(itemName))
                    return true;

                var item = FindItem(itemName);
                if (item == null)
                    return false;

                // Item directory exists if there's no part path
                if (string.IsNullOrEmpty(partPath))
                    return true;

                // Check if this is a subdirectory by seeing if any parts start with this path
                EnsureItemDefinitionLoaded(item.Id);
                var definition = _itemDefinitions[item.Id];
                var normalizedPartPath = NormalizePath(partPath) + Path.DirectorySeparatorChar;
                
                return definition.Parts?.Any(p => p.Path?.StartsWith(normalizedPartPath, StringComparison.OrdinalIgnoreCase) ?? false) ?? false;
            }
            catch
            {
                return false;
            }
        }

        public byte[] ReadAllBytes(string path)
        {
            var text = ReadAllText(path);
            return Encoding.UTF8.GetBytes(text);
        }

        public string ReadAllText(string path)
        {
            var (itemName, partPath) = ParsePath(path);
            
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(partPath))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var item = FindItem(itemName);
            if (item == null)
            {
                throw new FileNotFoundException($"Item not found: {itemName}");
            }

            var part = FindPart(item.Id, partPath);
            if (part == null)
            {
                throw new FileNotFoundException($"Part not found: {partPath} in item {itemName}");
            }

            // Decode base64 payload
            if (part.PayloadType == "InlineBase64" && !string.IsNullOrEmpty(part.Payload))
            {
                return DecodeBase64(part.Payload);
            }

            throw new NotSupportedException($"Unsupported payload type: {part.PayloadType}");
        }

        public void WriteAllText(string path, string contents)
        {
            throw new NotSupportedException("FabricFileSystem is read-only. Use Fabric REST API Update operations for modifications.");
        }

        public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var (itemName, partPath) = ParsePath(path);
            var results = new List<string>();

            if (string.IsNullOrEmpty(itemName))
            {
                // Root level - no files, only item directories
                return results;
            }

            var item = FindItem(itemName);
            if (item == null)
            {
                return results;
            }

            EnsureItemDefinitionLoaded(item.Id);
            var definition = _itemDefinitions[item.Id];

            if (definition.Parts == null)
                return results;

            var normalizedPartPath = NormalizePath(partPath);
            
            foreach (var part in definition.Parts)
            {
                if (string.IsNullOrEmpty(part.Path))
                    continue;

                var normalizedCurrentPath = NormalizePath(part.Path);
                var partDirectory = Path.GetDirectoryName(normalizedCurrentPath)?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar) ?? string.Empty;

                bool isMatch = false;
                if (searchOption == SearchOption.AllDirectories)
                {
                    // Match if part is in the directory or any subdirectory
                    isMatch = string.IsNullOrEmpty(normalizedPartPath) ||
                             partDirectory.Equals(normalizedPartPath, StringComparison.OrdinalIgnoreCase) ||
                             partDirectory.StartsWith(normalizedPartPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // Match only if part is directly in the directory
                    isMatch = partDirectory.Equals(normalizedPartPath, StringComparison.OrdinalIgnoreCase);
                }

                if (isMatch)
                {
                    // Check search pattern
                    if (string.IsNullOrEmpty(searchPattern) || searchPattern == "*" || MatchesPattern(Path.GetFileName(part.Path), searchPattern))
                    {
                        results.Add(Path.Combine(itemName, part.Path));
                    }
                }
            }

            return results;
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return GetFiles(path, "*", SearchOption.TopDirectoryOnly);
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            var (itemName, partPath) = ParsePath(path);
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(itemName))
            {
                // Root level - return all item names as directories
                EnsureWorkspaceItemsLoaded();
                foreach (var workspaceItem in _workspaceItems!)
                {
                    results.Add(workspaceItem.DisplayName);
                }
                return results.ToList();
            }

            var item = FindItem(itemName);
            if (item == null)
            {
                return new List<string>();
            }

            EnsureItemDefinitionLoaded(item.Id);
            var definition = _itemDefinitions[item.Id];

            if (definition.Parts == null)
                return new List<string>();

            var normalizedPartPath = NormalizePath(partPath);
            
            foreach (var part in definition.Parts)
            {
                if (string.IsNullOrEmpty(part.Path))
                    continue;

                var normalizedCurrentPath = NormalizePath(part.Path);
                var partDirectory = Path.GetDirectoryName(normalizedCurrentPath)?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar) ?? string.Empty;

                // Check if this part is in a subdirectory of the current path
                if (string.IsNullOrEmpty(normalizedPartPath))
                {
                    // Looking for top-level subdirectories within the item
                    if (!string.IsNullOrEmpty(partDirectory))
                    {
                        var firstSegment = partDirectory.Split(Path.DirectorySeparatorChar)[0];
                        results.Add(Path.Combine(itemName, firstSegment));
                    }
                }
                else if (partDirectory.StartsWith(normalizedPartPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // Part is in a subdirectory of the current path
                    var relativePath = partDirectory.Substring(normalizedPartPath.Length + 1);
                    var firstSegment = relativePath.Split(Path.DirectorySeparatorChar)[0];
                    results.Add(Path.Combine(itemName, normalizedPartPath, firstSegment));
                }
            }

            return results.ToList();
        }

        public string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            
            var normalizedPath = NormalizePath(path);
            var lastSeparatorIndex = normalizedPath.LastIndexOf(Path.DirectorySeparatorChar);
            
            return lastSeparatorIndex >= 0 ? normalizedPath.Substring(lastSeparatorIndex + 1) : normalizedPath;
        }

        public string GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            
            var normalizedPath = NormalizePath(path);
            var lastSeparatorIndex = normalizedPath.LastIndexOf(Path.DirectorySeparatorChar);
            
            return lastSeparatorIndex >= 0 ? normalizedPath.Substring(0, lastSeparatorIndex) : string.Empty;
        }

        public string GetExtension(string path)
        {
            var fileName = GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;
            
            var lastDotIndex = fileName.LastIndexOf('.');
            return lastDotIndex >= 0 ? fileName.Substring(lastDotIndex) : string.Empty;
        }

        public string PathCombine(params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                return string.Empty;
            
            var result = paths[0];
            for (int i = 1; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                    continue;
                
                result = result.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar
                    + paths[i].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            
            return NormalizePath(result);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Matches a filename against a wildcard pattern
        /// </summary>
        private bool MatchesPattern(string filename, string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || pattern == "*")
                return true;

            // Simple wildcard matching
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            
            return System.Text.RegularExpressions.Regex.IsMatch(filename, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Forcefully refreshes the workspace items cache
        /// </summary>
        public void RefreshWorkspaceItems()
        {
            lock (_itemsLock)
            {
                _workspaceItems = null;
                
                // If scoped to a single item, also refresh the item metadata
                if (_scopedItemId != null)
                {
                    var item = LoadItemMetadataAsync(_scopedItemId).GetAwaiter().GetResult();
                    _scopedItemName = item.DisplayName;
                }
            }
            EnsureWorkspaceItemsLoaded();
        }

        /// <summary>
        /// Forcefully refreshes a specific item's definition cache
        /// </summary>
        public void RefreshItemDefinition(string itemId)
        {
            lock (_definitionLocks)
            {
                _itemDefinitions.Remove(itemId);
            }
            EnsureItemDefinitionLoaded(itemId);
        }

        public long GetFileSize(string path)
        {
            var (itemName, partPath) = ParsePath(path);
            
            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(partPath))
            {
                throw new FileNotFoundException($"File not found: {path}");
            }

            var item = FindItem(itemName);
            if (item == null)
            {
                throw new FileNotFoundException($"Item not found: {itemName}");
            }

            var part = FindPart(item.Id, partPath);
            if (part == null)
            {
                throw new FileNotFoundException($"Part not found: {partPath} in item {itemName}");
            }

            // Calculate size from base64 payload
            if (part.PayloadType == "InlineBase64" && !string.IsNullOrEmpty(part.Payload))
            {
                // Base64 encoding: 4 characters represent 3 bytes
                // Account for padding characters
                var base64Length = part.Payload.Length;
                var paddingCount = part.Payload.EndsWith("==") ? 2 : (part.Payload.EndsWith("=") ? 1 : 0);
                return (base64Length * 3 / 4) - paddingCount;
            }

            throw new NotSupportedException($"Unsupported payload type: {part.PayloadType}");
        }

        public string GetFileNameWithoutExtension(string path)
        {
            var fileName = GetFileName(path);
            var extension = GetExtension(path);
            if (string.IsNullOrEmpty(extension))
                return fileName;
            return fileName.Substring(0, fileName.Length - extension.Length);
        }

        #endregion

        #region Data Models

        private class FabricItemsResponse
        {
            public List<FabricItem>? Value { get; set; }
        }

        private class FabricItem
        {
            public string Id { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string WorkspaceId { get; set; } = string.Empty;
        }

        private class FabricItemDefinitionResponse
        {
            public FabricItemDefinition? Definition { get; set; }
        }

        private class FabricItemDefinition
        {
            public List<FabricItemPart>? Parts { get; set; }
        }

        private class FabricItemPart
        {
            public string? Path { get; set; }
            public string? Payload { get; set; }
            public string? PayloadType { get; set; }
        }

        #endregion
    }
}
