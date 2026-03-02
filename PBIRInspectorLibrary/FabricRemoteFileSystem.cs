using Azure.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace PBIRInspectorLibrary
{
    /// <summary>
    /// File system implementation that uses Microsoft Fabric REST API to retrieve workspace items on demand.
    /// This implementation provides lazy loading of items and their definitions, only downloading base64 payloads when needed.
    /// Supports two instantiation modes:
    /// 1. Workspace-scoped: Access all items in a workspace
    /// 2. Item-scoped: Access only a specific item within a workspace
    /// 
    /// Long-Running Operation (LRO) Support:
    /// The getDefinition API may return HTTP 202 (Accepted) for asynchronous processing.
    /// This class automatically handles LRO polling following Microsoft's documented pattern:
    /// - Extracts Location, x-ms-operation-id, and Retry-After headers from 202 responses
    /// - Polls operation status respecting Retry-After header with exponential backoff (1s → 2s → 4s → max 10s)
    /// - Handles status values: NotStarted, Running, Succeeded, Failed
    /// - Retrieves final result from {Location}/result endpoint when operation succeeds
    /// - Configurable via maxLroAttempts, initialRetryDelayMs, and maxRetryDelayMs constructor parameters
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
    /// 
    /// // Custom LRO configuration
    /// var fs = new FabricFileSystem(workspaceId, credential, maxLroAttempts: 60, maxRetryDelayMs: 30000);
    /// </remarks>
    public class FabricRemoteFileSystem : IFabricFileSystem
    {
        private readonly string _workspaceId;
        private readonly TokenCredential _credential;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://api.fabric.microsoft.com/v1";
        
        // LRO configuration
        private readonly int _maxLroAttempts;
        private readonly int _initialRetryDelayMs;
        private readonly int _maxRetryDelayMs;
        
        // Scope tracking fields (null for workspace-scoped, set for item-scoped)
        private FabricItem _scopedItem;
        
        // Cache for workspace items (lazy loaded once)
        private List<FabricItem>? _workspaceItems;
        private readonly object _itemsLock = new object();
        
        // Cache for item definitions (lazy loaded per item)
        private readonly Dictionary<string, FabricItemDefinition> _itemDefinitions = new Dictionary<string, FabricItemDefinition>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _definitionLocks = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the root path for this file system instance
        /// </summary>
        public string RootPath => _scopedItem != null 
            ? $"/{BuildItemDirectoryPath(_scopedItem)}" 
            : "/";

        /// <summary>
        /// Initializes a new instance of the FabricFileSystem class for workspace-scoped access
        /// </summary>
        /// <param name="workspaceId">The Fabric workspace ID (GUID)</param>
        /// <param name="credential">Azure credential for authentication with automatic token refresh</param>
        /// <param name="httpClient">Optional HttpClient instance for testing/reuse</param>
        /// <param name="maxLroAttempts">Maximum number of polling attempts for long-running operations (default: 30)</param>
        /// <param name="initialRetryDelayMs">Initial retry delay in milliseconds (default: 1000, overridden by Retry-After header)</param>
        /// <param name="maxRetryDelayMs">Maximum retry delay in milliseconds for exponential backoff (default: 10000)</param>
        public FabricRemoteFileSystem(string workspaceId, TokenCredential credential, HttpClient? httpClient = null, 
            int maxLroAttempts = 30, int initialRetryDelayMs = 1000, int maxRetryDelayMs = 10000)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
                throw new ArgumentNullException(nameof(workspaceId));
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));

            _workspaceId = workspaceId;
            _credential = credential;
            _httpClient = httpClient ?? new HttpClient();
            //_scopedItem = null;
            _maxLroAttempts = maxLroAttempts;
            _initialRetryDelayMs = initialRetryDelayMs;
            _maxRetryDelayMs = maxRetryDelayMs;
            
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
        /// <param name="maxLroAttempts">Maximum number of polling attempts for long-running operations (default: 30)</param>
        /// <param name="initialRetryDelayMs">Initial retry delay in milliseconds (default: 1000, overridden by Retry-After header)</param>
        /// <param name="maxRetryDelayMs">Maximum retry delay in milliseconds for exponential backoff (default: 10000)</param>
        public FabricRemoteFileSystem(string workspaceId, string itemId, TokenCredential credential, HttpClient? httpClient = null,
            int maxLroAttempts = 30, int initialRetryDelayMs = 1000, int maxRetryDelayMs = 10000)
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
            //_scopedItemId = itemId;
            _maxLroAttempts = maxLroAttempts;
            _initialRetryDelayMs = initialRetryDelayMs;
            _maxRetryDelayMs = maxRetryDelayMs;
            
            // Configure default headers
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Eagerly load item metadata to validate itemId and cache item name
            _scopedItem = LoadItemMetadataAsync(itemId).GetAwaiter().GetResult();
            _scopedItem.DirectoryPath = BuildItemDirectoryPath(_scopedItem); // Set directory path to root for item-scoped access
            //_scopedItemName = item.DisplayName;
        }

        #region Private Helper Methods

        /// <summary>
        /// Gets an access token from the credential and configures HTTP client authorization
        /// </summary>
        private async Task EnsureAuthenticatedAsync()
        {
            var tokenRequestContext = new TokenRequestContext(new[] { "https://api.fabric.microsoft.com/Workspace.Read.All", "https://api.fabric.microsoft.com/Item.ReadWrite.All" });
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
                if (_scopedItem != null)
                {
                    _workspaceItems = new List<FabricItem>
                    {
                        _scopedItem
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

            result.DirectoryPath = BuildItemDirectoryPath(result);

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

            result?.Value?.ForEach(item => item.DirectoryPath = BuildItemDirectoryPath(item));

            return result?.Value ?? new List<FabricItem>();
        }


        private string BuildItemName(FabricItem item)
        {
            if (item == null)
                return string.Empty;
            return string.Concat(item.DisplayName, ".", item.Type);
        }

        private string BuildItemDirectoryPath(FabricItem item)
        {
            if (item == null)
                return string.Empty;
            //TODO: prefix with workspace folder recursive path
            EnsureWorkspaceFolderPathsLoaded();
            //var folderPath = _workspaceFolderPaths.ContainsKey(item.Id) ? _workspaceFolderPaths[item.Id] : string.Empty;
            var folderPath = string.Empty; // For now, we don't have folder path information, so this will be empty. In the future, we can build this from the item's parent folders.
            return NormalizePath(string.Concat(Path.AltDirectorySeparatorChar, folderPath, BuildItemName(item)));
        }

        private void EnsureWorkspaceFolderPathsLoaded()
        {
            // This method would implement logic to load folder paths for items if the API provides that information.
            // For now, it's a placeholder since we don't have folder path data in the current item model.
            // In the future, we could call an API endpoint to get folder structures and build a mapping of itemId to folderPath.
            
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

                var definition = LoadItemDefinitionAsync(itemId, CancellationToken.None).GetAwaiter().GetResult();
                _itemDefinitions[itemId] = definition;
            }
        }

        /// <summary>
        /// Loads a specific item's definition from Fabric REST API
        /// Handles HTTP 202 (Accepted) responses by polling until the operation completes.
        /// Follows the Microsoft Fabric REST API long-running operation (LRO) pattern:
        /// - Extracts Location, x-ms-operation-id, and Retry-After headers from 202 response
        /// - Polls the operation status URL respecting Retry-After with exponential backoff
        /// - Handles Running, Succeeded, and Failed status values
        /// - Retrieves final result from {Location}/result when status is Succeeded
        /// </summary>
        /// <param name="itemId">The item ID to get definition for</param>
        /// <param name="cancellationToken">Cancellation token to stop polling</param>
        private async Task<FabricItemDefinition> LoadItemDefinitionAsync(string itemId, CancellationToken cancellationToken = default)
        {
            await EnsureAuthenticatedAsync();
            
            var url = $"{_baseUrl}/workspaces/{_workspaceId}/items/{itemId}/getDefinition";
            var initialResponse = await _httpClient.PostAsync(url, null, cancellationToken);
            
            // Handle HTTP 202 (Accepted) - operation is asynchronous
            if (initialResponse.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                // Extract required headers from LRO response
                var locationHeader = initialResponse.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(locationHeader))
                {
                    throw new HttpRequestException($"Received HTTP 202 but no Location header for item {itemId}");
                }

                // Extract optional headers for diagnostics and retry timing
                var operationId = initialResponse.Headers.TryGetValues("x-ms-operation-id", out var opIdValues) 
                    ? opIdValues.FirstOrDefault() 
                    : null;
                
                var retryAfterSeconds = initialResponse.Headers.RetryAfter?.Delta?.TotalSeconds 
                    ?? (_initialRetryDelayMs / 1000.0);

                var pollingUri = new Uri(locationHeader, UriKind.RelativeOrAbsolute);
                if (!pollingUri.IsAbsoluteUri)
                {
                    pollingUri = new Uri(new Uri(_baseUrl), locationHeader);
                }

                // Poll until operation completes
                int currentDelayMs = Math.Max(_initialRetryDelayMs, (int)(retryAfterSeconds * 1000));
                bool operationComplete = false;
                HttpResponseMessage? resultResponse = null;
                
                for (int attempt = 0; attempt < _maxLroAttempts; attempt++)
                {
                    await Task.Delay(currentDelayMs, cancellationToken);
                    
                    var pollResponse = await _httpClient.GetAsync(pollingUri, cancellationToken);

                    if (pollResponse.IsSuccessStatusCode)
                    {
                        var pollContent = await pollResponse.Content.ReadAsStringAsync();

                        // Try to deserialize as operation status
                        FabricOperationResult? operationResult;
                        try
                        {
                            operationResult = JsonSerializer.Deserialize<FabricOperationResult>(pollContent, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });
                        }
                        catch (JsonException)
                        {
                            // If we can't deserialize as operation result, this might be the actual result
                            // This can happen if the operation completed synchronously
                            operationResult = null;
                        }

                        if (operationResult != null)
                        {
                            // Check operation status
                            if (string.Equals(operationResult.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                // Operation succeeded - get the actual result
                                var resultUri = new Uri(pollingUri.ToString() + "/result");
                                resultResponse = await _httpClient.GetAsync(resultUri, cancellationToken);
                                operationComplete = true;
                                break;
                            }
                            else if (string.Equals(operationResult.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                            {
                                // Operation failed - throw with error details
                                var errorMessage = operationResult.Error?.Message ?? "Operation failed";
                                var errorCode = operationResult.Error?.Code ?? "Unknown";
                                var errorDetails = operationResult.Error?.Details ?? string.Empty;
                                throw new HttpRequestException(
                                    $"Long-running operation failed for item {itemId}. " +
                                    $"Error Code: {errorCode}, Message: {errorMessage}, Details: {errorDetails}");
                            }
                            else if (string.Equals(operationResult.Status, "Running", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(operationResult.Status, "NotStarted", StringComparison.OrdinalIgnoreCase))
                            {
                                // Still running - continue polling with exponential backoff
                                currentDelayMs = Math.Min(currentDelayMs * 2, _maxRetryDelayMs);
                                continue;
                            }
                            else
                            {
                                // Unknown status - treat as error
                                throw new HttpRequestException(
                                    $"Unknown operation status '{operationResult.Status}' for item {itemId}");
                            }
                        }
                        else
                        {
                            // Could not deserialize as operation result - assume this is the final result
                            resultResponse = pollResponse;
                            operationComplete = true;
                            break;
                        }
                    }
                    else if (pollResponse.StatusCode == System.Net.HttpStatusCode.Accepted)
                    {
                        // Still processing - continue polling
                        currentDelayMs = Math.Min(currentDelayMs * 2, _maxRetryDelayMs);
                        continue;
                    }
                    else
                    {
                        // Error occurred during polling
                        var errorContent = await pollResponse.Content.ReadAsStringAsync();
                        throw new HttpRequestException(
                            $"Failed while polling item definition for {itemId}: {pollResponse.StatusCode} - {errorContent}");
                    }
                }
                
                // Check if we exceeded max attempts
                if (!operationComplete)
                {
                    throw new TimeoutException(
                        $"Timed out waiting for item definition for {itemId} after {_maxLroAttempts} attempts. " +
                        $"Operation ID: {operationId ?? "unknown"}");
                }

                // Use the result response
                if (resultResponse == null || !resultResponse.IsSuccessStatusCode)
                {
                    var errorContent = resultResponse != null 
                        ? await resultResponse.Content.ReadAsStringAsync() 
                        : "No response";
                    throw new HttpRequestException(
                        $"Failed to get operation result for item {itemId}: {resultResponse?.StatusCode} - {errorContent}");
                }

                initialResponse = resultResponse;
            }
            
            // Handle synchronous success (200 OK) or final result from LRO
            if (!initialResponse.IsSuccessStatusCode)
            {
                var errorContent = await initialResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Failed to load item definition for {itemId}: {initialResponse.StatusCode} - {errorContent}");
            }

            var json = await initialResponse.Content.ReadAsStringAsync();
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
            var segments = normalizedPath.Split(new[] { Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            
            if (segments.Length == 0)
                return (string.Empty, string.Empty);
            
            if (segments.Length == 1)
                return (segments[0], string.Empty);
            
            return (segments[0], string.Join(Path.AltDirectorySeparatorChar.ToString(), segments.Skip(1)));
        }

        /// <summary>
        /// Finds an item by name (case-insensitive)
        /// </summary>
        private FabricItem? FindItem(string itemName)
        { 
            EnsureWorkspaceItemsLoaded();
            
            // If scoped to a single item, only return it if the name matches
            if (_scopedItem != null && !string.Equals(BuildItemName(_scopedItem), itemName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            
            return _workspaceItems!.FirstOrDefault(i => string.Equals(BuildItemName(i), itemName, StringComparison.OrdinalIgnoreCase));
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

            path = path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            //path = path.TrimStart(Path.DirectorySeparatorChar);
            //path = path.TrimEnd(Path.DirectorySeparatorChar);

            path = path.TrimStart(Path.AltDirectorySeparatorChar);
            path = path.TrimEnd(Path.AltDirectorySeparatorChar);

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
                var normalizedPartPath = NormalizePath(partPath) + Path.AltDirectorySeparatorChar;
                
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
                var partDirectory = NormalizePath(Path.GetDirectoryName(normalizedCurrentPath)) ?? string.Empty;

                bool isMatch = false;
                if (searchOption == SearchOption.AllDirectories)
                {
                    // Match if part is in the directory or any subdirectory
                    isMatch = string.IsNullOrEmpty(normalizedPartPath) ||
                             partDirectory.Equals(normalizedPartPath, StringComparison.OrdinalIgnoreCase) ||
                             partDirectory.StartsWith(normalizedPartPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
                        results.Add(NormalizePath(Path.Combine(itemName, part.Path)));
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
                var partDirectory = NormalizePath(Path.GetDirectoryName(normalizedCurrentPath)) ?? string.Empty;

                // Check if this part is in a subdirectory of the current path
                if (string.IsNullOrEmpty(normalizedPartPath))
                {
                    // Looking for top-level subdirectories within the item
                    if (!string.IsNullOrEmpty(partDirectory))
                    {
                        var firstSegment = partDirectory.Split(Path.AltDirectorySeparatorChar)[0];
                        results.Add(NormalizePath(PathCombine(itemName, firstSegment)));
                    }
                }
                else if (partDirectory.StartsWith(normalizedPartPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // Part is in a subdirectory of the current path
                    var relativePath = partDirectory.Substring(normalizedPartPath.Length + 1);
                    var firstSegment = relativePath.Split(Path.AltDirectorySeparatorChar)[0];
                    results.Add(NormalizePath(PathCombine(itemName, normalizedPartPath, firstSegment)));
                }
            }

            return results.ToList();
        }

        public string GetFileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            
            var normalizedPath = NormalizePath(path);
            var lastSeparatorIndex = normalizedPath.LastIndexOf(Path.AltDirectorySeparatorChar);
            
            return lastSeparatorIndex >= 0 ? normalizedPath.Substring(lastSeparatorIndex + 1) : normalizedPath;
        }

        public string GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            
            var normalizedPath = NormalizePath(path);
            var lastSeparatorIndex = normalizedPath.LastIndexOf(Path.AltDirectorySeparatorChar);
            
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
                
                result = result.TrimEnd(Path.AltDirectorySeparatorChar)
                    + Path.AltDirectorySeparatorChar
                    + paths[i].TrimStart(Path.AltDirectorySeparatorChar);
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
                if (_scopedItem != null)
                {
                    var item = LoadItemMetadataAsync(_scopedItem.Id).GetAwaiter().GetResult();
                    _scopedItem = item;
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

        public IEnumerable<FabricItem> GetFabricItems(string path)
        {
            var fabricItems = new List<FabricItem>();
            var (itemName, partPath) = ParsePath(path);
            EnsureWorkspaceItemsLoaded();
            if (string.IsNullOrEmpty(itemName))
            {
                // Root level - return all items
                fabricItems.AddRange(_workspaceItems!);
            }
            else
            {
                var item = FindItem(itemName);
                if (item != null)
                {
                    fabricItems.Add(item);
                }
            }
            return fabricItems;
        }

        #endregion

        #region Data Models

        private class FabricItemsResponse
        {
            public List<FabricItem>? Value { get; set; }
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

        /// <summary>
        /// Represents the result of a Fabric REST API long-running operation (LRO) status check.
        /// Used when polling operation status after receiving HTTP 202 Accepted.
        /// Status values: NotStarted, Running, Succeeded, Failed
        /// </summary>
        private class FabricOperationResult
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;
            
            [JsonPropertyName("createdTimeUtc")]
            public DateTimeOffset? CreatedTimeUtc { get; set; }

            [JsonPropertyName("lastUpdatedTimeUtc")]
            public DateTimeOffset? LastUpdatedTimeUtc { get; set; }

            [JsonPropertyName("percentComplete")]
            public int? PercentComplete { get; set; }

            [JsonPropertyName("error")]
            public FabricOperationError? Error { get; set; }
        }

        /// <summary>
        /// Represents error details from a failed long-running operation.
        /// Populated in FabricOperationResult when Status is "Failed".
        /// </summary>
        private class FabricOperationError
        {
            [JsonPropertyName("code")]
            public string? Code { get; set; }
            
            [JsonPropertyName("message")]
            public string? Message { get; set; }
            
            [JsonPropertyName("details")]
            public string? Details { get; set; }
        }

        #endregion
    }
}
