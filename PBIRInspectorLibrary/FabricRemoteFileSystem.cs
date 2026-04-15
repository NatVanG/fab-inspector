using Azure.Core;
using System;
using System.Collections.Concurrent;
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
        
        // Token caching (#1)
        private AccessToken _cachedToken;
        private bool _tokenInitialized;
        private readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);
        
        // Scope tracking fields (null for workspace-scoped, set for item-scoped)
        private FabricItem _scopedItem;
        private string? _scopedItemName; // Cached built name (#6)

        // Cache for workspace items (lazy loaded once)
        private List<FabricItem>? _workspaceItems;
        private Dictionary<string, FabricItem>? _workspaceItemsByPath; // Indexed lookup (#2)
        private readonly object _itemsLock = new object();
        
        // Cache for item definitions (lazy loaded per item) - uses ConcurrentDictionary + Lazy (#5)
        private readonly ConcurrentDictionary<string, Lazy<FabricItemDefinition>> _itemDefinitions = new ConcurrentDictionary<string, Lazy<FabricItemDefinition>>(StringComparer.OrdinalIgnoreCase);
        
        // Cache for workspace folder paths (lazy loaded once)
        private Dictionary<string, string>? _workspaceFolderPaths;
        private readonly object _folderPathsLock = new object();

        /// <summary>
        /// Gets the root path for this file system instance
        /// </summary>
        public string RootPath => _scopedItem != null 
            ? $"/{BuildItemDirectoryPath(_scopedItem)}" 
            : "/";

        public IEnumerable<string>? ScopedItemTypes { get; set; }

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
            var item = LoadItemMetadataAsync(itemId).GetAwaiter().GetResult();
            SetScopedItem(item);
        }

        /// <summary>
        /// Creates an item-scoped file system instance without blocking the caller thread.
        /// </summary>
        public static async Task<FabricRemoteFileSystem> CreateItemScopedAsync(
            string workspaceId,
            string itemId,
            TokenCredential credential,
            HttpClient? httpClient = null,
            int maxLroAttempts = 30,
            int initialRetryDelayMs = 1000,
            int maxRetryDelayMs = 10000)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentNullException(nameof(itemId));

            var fileSystem = new FabricRemoteFileSystem(
                workspaceId,
                credential,
                httpClient,
                maxLroAttempts,
                initialRetryDelayMs,
                maxRetryDelayMs);

            var item = await fileSystem.LoadItemMetadataAsync(itemId).ConfigureAwait(false);
            fileSystem.SetScopedItem(item);

            return fileSystem;
        }

        #region Private Helper Methods

        private void SetScopedItem(FabricItem item)
        {
            _scopedItem = item;
            _scopedItem.DirectoryPath = BuildItemDirectoryPath(_scopedItem); // Set directory path to root for item-scoped access
            _scopedItemName = BuildItemName(_scopedItem); // Cache built name to avoid repeated string concat (#6)
        }

        /// <summary>
        /// Gets an access token from the credential and configures HTTP client authorization.
        /// Caches token and only refreshes when within 5 minutes of expiry (#1).
        /// </summary>
        private async Task EnsureAuthenticatedAsync()
        {
            // Fast path: token is valid with 5-minute buffer
            if (_tokenInitialized && _cachedToken.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return;
            }

            await _tokenSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (_tokenInitialized && _cachedToken.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
                {
                    return;
                }

                var tokenRequestContext = new TokenRequestContext(new[] { "https://api.fabric.microsoft.com/.default" });
                _cachedToken = await _credential.GetTokenAsync(tokenRequestContext, default).ConfigureAwait(false);
                _tokenInitialized = true;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken.Token);
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        /// <summary>
        /// Ensures workspace items are loaded (lazy loading).
        /// Also builds an indexed dictionary for O(1) path lookups (#2).
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

                // Build indexed lookup for O(1) ParsePath and FindItem lookups (#2)
                _workspaceItemsByPath = new Dictionary<string, FabricItem>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _workspaceItems)
                {
                    if (!string.IsNullOrEmpty(item.DirectoryPath))
                    {
                        var key = NormalizePath(item.DirectoryPath);
                        _workspaceItemsByPath[key] = item;
                    }
                }
            }
        }

        /// <summary>
        /// Loads a specific item's metadata from Fabric REST API
        /// </summary>
        private async Task<FabricItem> LoadItemMetadataAsync(string itemId)
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            
            var url = $"{_baseUrl}/workspaces/{_workspaceId}/items/{itemId}";
            var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to load item metadata for {itemId}: {response.StatusCode} - {await response.Content.ReadAsStringAsync().ConfigureAwait(false)}");
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        private async Task<List<FabricItem>> LoadWorkspaceItemsAsync()
        {
            var items = new List<FabricItem>();
            string[] ignoreItemTypes = [ "none" ];


            if (this.ScopedItemTypes == null || this.ScopedItemTypes.Contains("*")) // If no specific item types provided, load all items
            {
                items = await LoadWorkspaceItemsByTypeAsync().ConfigureAwait(false);
            }
            else
            {
                foreach (var itemType in this.ScopedItemTypes)
                {
                    if (string.IsNullOrWhiteSpace(itemType) || ignoreItemTypes.Contains(itemType, StringComparer.OrdinalIgnoreCase))
                        continue;
                    var itemsOfType = await LoadWorkspaceItemsByTypeAsync(itemType).ConfigureAwait(false);
                    if (itemsOfType != null)
                    {
                        itemsOfType.ForEach(item => item.DirectoryPath = BuildItemDirectoryPath(item));
                        items.AddRange(itemsOfType);
                    }
                }
            }

            return items;
        }

        /// <summary>
        /// Loads all items in the workspace from Fabric REST API, handling pagination via continuation tokens.
        /// </summary>
        private async Task<List<FabricItem>> LoadWorkspaceItemsByTypeAsync(string? itemType = null)
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);

            if (!string.IsNullOrEmpty(itemType) && itemType.Equals("report_deprecated", StringComparison.OrdinalIgnoreCase))
            {
                itemType = "report";
            }

            var allItems = new List<FabricItem>();
            string? nextUrl = $"{_baseUrl}/workspaces/{_workspaceId}/items";

            if (!string.IsNullOrEmpty(itemType))
            {
                nextUrl += $"?type={Uri.EscapeDataString(itemType)}";
            }

            while (nextUrl != null)
            {
                var response = await _httpClient.GetAsync(nextUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Failed to load workspace items: {response.StatusCode} - {await response.Content.ReadAsStringAsync().ConfigureAwait(false)}");
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<FabricItemsResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Value != null)
                {
                    result.Value.ForEach(item => item.DirectoryPath = BuildItemDirectoryPath(item));
                    allItems.AddRange(result.Value);
                }

                if (!string.IsNullOrEmpty(result?.ContinuationUri))
                {
                    nextUrl = result.ContinuationUri;
                }
                else if (!string.IsNullOrEmpty(result?.ContinuationToken))
                {
                    nextUrl = $"{_baseUrl}/workspaces/{_workspaceId}/items?continuationToken={Uri.EscapeDataString(result.ContinuationToken)}";
                }
                else
                {
                    nextUrl = null;
                }
            }

            return allItems;
        }


        private string BuildItemName(FabricItem item)
        {
            if (item == null)
                return string.Empty;
            return string.Concat(item.DisplayName, ".", item.Type);
        }

        #region folderLogic
        private string BuildItemDirectoryPath(FabricItem item)
        {
            if (item == null)
                return string.Empty;
            
            // Only load folder paths when item actually has a folder (#4)
            var folderPath = string.Empty;
            if (!string.IsNullOrEmpty(item.FolderId))
            {
                EnsureWorkspaceFolderPathsLoaded();
                if (_workspaceFolderPaths != null && 
                    _workspaceFolderPaths.TryGetValue(item.FolderId, out var path))
                {
                    folderPath = path + Path.AltDirectorySeparatorChar;
                }
            }
            
            return NormalizePath(string.Concat(Path.AltDirectorySeparatorChar, folderPath, BuildItemName(item)));
        }

        private void EnsureWorkspaceFolderPathsLoaded()
        {
            if (_workspaceFolderPaths != null)
                return;

            lock (_folderPathsLock)
            {
                if (_workspaceFolderPaths != null)
                    return;

                _workspaceFolderPaths = LoadWorkspaceFolderPathsAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Loads all folders in the workspace from Fabric REST API and builds hierarchical paths,
        /// handling pagination via continuation tokens.
        /// </summary>
        private async Task<Dictionary<string, string>> LoadWorkspaceFolderPathsAsync()
        {
            await EnsureAuthenticatedAsync().ConfigureAwait(false);

            var allFolders = new List<FabricFolder>();
            string? nextUrl = $"{_baseUrl}/workspaces/{_workspaceId}/folders";

            while (nextUrl != null)
            {
                var response = await _httpClient.GetAsync(nextUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    // If folders API is not available or fails, return empty dictionary
                    // This ensures backward compatibility if the API endpoint doesn't exist
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<FabricFoldersResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Value != null)
                {
                    allFolders.AddRange(result.Value);
                }

                if (!string.IsNullOrEmpty(result?.ContinuationUri))
                {
                    nextUrl = result.ContinuationUri;
                }
                else if (!string.IsNullOrEmpty(result?.ContinuationToken))
                {
                    nextUrl = $"{_baseUrl}/workspaces/{_workspaceId}/folders?continuationToken={Uri.EscapeDataString(result.ContinuationToken)}";
                }
                else
                {
                    nextUrl = null;
                }
            }

            // Build hierarchical paths
            var folderPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Create a lookup dictionary for quick parent access
            var folderLookup = allFolders.ToDictionary(f => f.Id, f => f, StringComparer.OrdinalIgnoreCase);

            // Build path for each folder
            foreach (var folder in allFolders)
            {
                folderPaths[folder.Id] = BuildFolderPath(folder, folderLookup);
            }

            return folderPaths;
        }

        /// <summary>
        /// Recursively builds the hierarchical path for a folder
        /// </summary>
        private string BuildFolderPath(FabricFolder folder, Dictionary<string, FabricFolder> folderLookup)
        {
            const string rootFolderName = "rootfolder";
            if (string.IsNullOrEmpty(folder.ParentFolderId))
            {
                // Root folder - return just the folder name
                return folder.DisplayName.Replace(rootFolderName, "");
            }
            
            if (folderLookup.TryGetValue(folder.ParentFolderId, out var parentFolder))
            {
                // Build parent path recursively and append current folder
                var parentPath = BuildFolderPath(parentFolder, folderLookup);
                return $"{parentPath}{Path.AltDirectorySeparatorChar}{folder.DisplayName}";
            }
            
            // Parent not found, treat as root folder
            return folder.DisplayName.Replace(rootFolderName, "");
        }
        #endregion

        /// <summary>
        /// Ensures a specific item's definition is loaded (lazy loading per item).
        /// Uses ConcurrentDictionary + Lazy for lock-free thread safety (#5).
        /// </summary>
        private void EnsureItemDefinitionLoaded(string itemId)
        {
            _itemDefinitions.GetOrAdd(itemId, id =>
                new Lazy<FabricItemDefinition>(() =>
                    LoadItemDefinitionAsync(id, CancellationToken.None).GetAwaiter().GetResult()));
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
            await EnsureAuthenticatedAsync().ConfigureAwait(false);
            
            var url = $"{_baseUrl}/workspaces/{_workspaceId}/items/{itemId}/getDefinition";
            var initialResponse = await _httpClient.PostAsync(url, null, cancellationToken).ConfigureAwait(false);

            // Handle BadRequest "errorCode":"OperationNotSupportedForItem"
            if (initialResponse.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorContent = await initialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (errorContent.Contains("OperationNotSupportedForItem", StringComparison.OrdinalIgnoreCase))
                {
                    if (_scopedItem != null)
                    {
                        throw new NotSupportedException($"GetDefinition operation is not supported for item {itemId}: {errorContent}");
                    }
                    else
                    {
                        //TODO: skip gracefully and carry on inspecting the rest of the workspace items
                        return new FabricItemDefinition { Parts = new List<FabricItemPart>() };
                    }
                }
                throw new HttpRequestException($"Failed to load item definition for {itemId}: {initialResponse.StatusCode} - {errorContent}");
            }

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
                    await Task.Delay(currentDelayMs, cancellationToken).ConfigureAwait(false);
                    
                    var pollResponse = await _httpClient.GetAsync(pollingUri, cancellationToken).ConfigureAwait(false);

                    if (pollResponse.IsSuccessStatusCode)
                    {
                        var pollContent = await pollResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

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
                            // TODO: Handle continuation token
                            // Check operation status
                            if (string.Equals(operationResult.Status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                            {
                                // Operation succeeded - get the actual result
                                var resultUri = new Uri(pollingUri.ToString() + "/result");
                                resultResponse = await _httpClient.GetAsync(resultUri, cancellationToken).ConfigureAwait(false);
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
                        var errorContent = await pollResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
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
                        ? await resultResponse.Content.ReadAsStringAsync().ConfigureAwait(false) 
                        : "No response";
                    throw new HttpRequestException(
                        $"Failed to get operation result for item {itemId}: {resultResponse?.StatusCode} - {errorContent}");
                }

                initialResponse = resultResponse;
            }
            
            // Handle synchronous success (200 OK) or final result from LRO
            if (!initialResponse.IsSuccessStatusCode)
            {
                var errorContent = await initialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Failed to load item definition for {itemId}: {initialResponse.StatusCode} - {errorContent}");
            }

            var json = await initialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<FabricItemDefinitionResponse>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            return result?.Definition ?? new FabricItemDefinition { Parts = new List<FabricItemPart>() };
        }

        /// <summary>
        /// Parses a virtual path into item name and part path
        /// If path starts with a known _workspaceItem DirectoryPath, splits there
        /// Otherwise splits on item type suffixes (e.g., .Report, .CopyJob)
        /// </summary>
        private (string itemName, string partPath) ParsePath(string path)
        {
            EnsureWorkspaceItemsLoaded();

            var normalizedPath = NormalizePath(path);

            // If path is empty or root, return empty values
            if (string.IsNullOrEmpty(normalizedPath))
                return (string.Empty, string.Empty);

            // Try exact match first - O(1) dictionary lookup (#2)
            if (_workspaceItemsByPath!.ContainsKey(normalizedPath))
                return (normalizedPath, string.Empty);

            // Try prefix match: progressively shorten path to find item boundary
            var separatorIndex = normalizedPath.Length;
            while ((separatorIndex = normalizedPath.LastIndexOf(Path.AltDirectorySeparatorChar, separatorIndex - 1)) > 0)
            {
                var candidate = normalizedPath.Substring(0, separatorIndex);
                if (_workspaceItemsByPath.ContainsKey(candidate))
                {
                    var partPath = normalizedPath.Substring(separatorIndex + 1);
                    return (candidate, partPath);
                }
            }

            // Fallback: If no workspace item match found, use legacy splitting logic
            // This handles cases where items might not be loaded or path format is different
            var segments = normalizedPath.Split(new[] { Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                return (string.Empty, string.Empty);

            if (segments.Length == 1)
                return (segments[0], string.Empty);

            return (segments[0], string.Join(Path.AltDirectorySeparatorChar.ToString(), segments.Skip(1)));
        }

        /// <summary>
        /// Finds an item by name (case-insensitive).
        /// Callers must ensure EnsureWorkspaceItemsLoaded has been called (#3).
        /// Uses cached name (#6) and dictionary lookup (#2).
        /// </summary>
        private FabricItem? FindItem(string itemName)
        { 
            // Fast path for scoped item - uses cached name (#6)
            if (_scopedItem != null)
            {
                return string.Equals(_scopedItem.DirectoryPath, itemName, StringComparison.OrdinalIgnoreCase) 
                    || string.Equals(_scopedItemName, itemName, StringComparison.OrdinalIgnoreCase)
                    ? _scopedItem 
                    : null;
            }
            
            // O(1) dictionary lookup instead of linear scan (#2)
            if (_workspaceItemsByPath != null && _workspaceItemsByPath.TryGetValue(itemName, out var item))
            {
                return item;
            }
            
            return null;
        }

        /// <summary>
        /// Finds a part within an item's definition
        /// </summary>
        private FabricItemPart? FindPart(string itemId, string partPath)
        {
            EnsureItemDefinitionLoaded(itemId);
            var definition = _itemDefinitions[itemId].Value;
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
                var definition = _itemDefinitions[item.Id].Value;
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
            // TODO: cache decoded base64 payload?
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
            var definition = _itemDefinitions[item.Id].Value;

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
            var definition = _itemDefinitions[item.Id].Value;

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
                _workspaceItemsByPath = null; // Clear indexed lookup (#2)
                
                // If scoped to a single item, also refresh the item metadata
                if (_scopedItem != null)
                {
                    var item = LoadItemMetadataAsync(_scopedItem.Id).GetAwaiter().GetResult();
                    _scopedItem = item;
                    _scopedItemName = BuildItemName(_scopedItem); // Refresh cached name (#6)
                }
            }
            EnsureWorkspaceItemsLoaded();
        }

        /// <summary>
        /// Forcefully refreshes a specific item's definition cache
        /// </summary>
        public void RefreshItemDefinition(string itemId)
        {
            _itemDefinitions.TryRemove(itemId, out _); // Thread-safe removal (#5)
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

        //public IEnumerable<FabricItem> GetFabricItems(string path, IEnumerable<string> itemTypes)
        //{
        //    var fabricItems = new List<FabricItem>();
        //    var (itemName, partPath) = ParsePath(path);
        //    EnsureWorkspaceItemsLoaded(itemTypes);
        //    if (string.IsNullOrEmpty(itemName))
        //    {
        //        // Root level - return all items
        //        fabricItems.AddRange(_workspaceItems!);
        //    }
        //    else
        //    {
        //        var item = FindItem(itemName);
        //        if (item != null)
        //        {
        //            fabricItems.Add(item);
        //        }
        //    }
        //    return fabricItems;
        //}

        public string GetRelativePath(string fullPath)
        {
            return fullPath; // In this implementation, we treat all paths as relative to the workspace root, so we can return the full path as-is.
        }



        #endregion

        #region Data Models

        private class FabricItemsResponse
        {
            public List<FabricItem>? Value { get; set; }
            public string? ContinuationToken { get; set; }
            public string? ContinuationUri { get; set; }
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

        /// <summary>
        /// Represents the response from the Fabric REST API folders list endpoint
        /// </summary>
        private class FabricFoldersResponse
        {
            public List<FabricFolder>? Value { get; set; }
            public string? ContinuationToken { get; set; }
            public string? ContinuationUri { get; set; }
        }

        /// <summary>
        /// Represents a folder in a Fabric workspace
        /// </summary>
        private class FabricFolder
        {
            public string Id { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string? ParentFolderId { get; set; }
        }

        #endregion
    }
}
