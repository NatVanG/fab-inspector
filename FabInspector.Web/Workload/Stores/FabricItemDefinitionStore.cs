using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FabInspector.Core;
using FabInspector.Web.Workload.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FabInspector.Web.Workload.Stores;

/// <summary>
/// Options bound from <c>Workload:Items</c>.
/// </summary>
public sealed class FabricItemStoreOptions
{
    /// <summary>Cache TTL (seconds) for fetched definitions. 0 disables the cache.</summary>
    public int CacheTtlSeconds { get; set; } = 60;

    /// <summary>Maximum time to wait for a Fabric long-running operation to complete.</summary>
    public int LongRunningTimeoutSeconds { get; set; } = 60;

    /// <summary>Polling interval (seconds) for Fabric long-running operations when no Retry-After is supplied.</summary>
    public int LongRunningPollSeconds { get; set; } = 2;

    /// <summary>Base URL of the Fabric Items REST API. Override only for sovereign clouds.</summary>
    public string FabricApiBaseUrl { get; set; } = "https://api.fabric.microsoft.com/v1";
}

/// <summary>
/// <see cref="IItemDefinitionStore"/> backed by the Fabric Items REST API.
/// Item definitions live in OneLake under each workspace; this store calls
/// <c>getDefinition</c> / <c>updateDefinition</c> via On-Behalf-Of-acquired
/// Fabric tokens and holds nothing authoritative in process (only a short
/// <see cref="IMemoryCache"/> entry to keep job runs fast).
/// <para>
/// Soft-delete and restore are no-ops — Fabric owns the lifecycle and the
/// workload simply invalidates its cache.
/// </para>
/// </summary>
internal sealed class FabricItemDefinitionStore : IItemDefinitionStore
{
    private static readonly HttpClient _http = new()
    {
        // Conservative default; per-call cancellation tokens still apply.
        Timeout = TimeSpan.FromSeconds(100)
    };

    private readonly ITokenProvider _tokenProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FabricItemDefinitionStore> _logger;
    private readonly FabricItemStoreOptions _options;

    public FabricItemDefinitionStore(
        ITokenProvider tokenProvider,
        IMemoryCache cache,
        IOptions<FabricItemStoreOptions> options,
        ILogger<FabricItemDefinitionStore> logger)
    {
        _tokenProvider = tokenProvider;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    private static string CacheKey(string itemType, Guid workspaceId, Guid itemId) =>
        $"fab-item-def::{itemType}::{workspaceId:N}::{itemId:N}";

    public Task UpsertAsync(
        string itemType,
        Guid workspaceId,
        Guid itemId,
        ItemDefinitionEnvelope envelope,
        string? ifMatch = null,
        CancellationToken ct = default)
    {
        // Lifecycle notifications (Create/Update) are informational: Fabric is
        // already the source of truth and has persisted the definition before
        // calling us. Writing back via updateDefinition would be a redundant
        // round-trip and risks overwriting newer state. We therefore drop our
        // cached copy and let the next GetAsync re-fetch from OneLake.
        //
        // The ifMatch parameter is intentionally ignored for the Fabric-backed
        // store — Fabric performs its own optimistic-concurrency check on the
        // editor's save path before raising the lifecycle event.
        _ = envelope;
        _ = ifMatch;
        _cache.Remove(CacheKey(itemType, workspaceId, itemId));
        _logger.LogInformation(
            "Cache evicted for {ItemType}/{Workspace}/{Item} (Fabric lifecycle notification)",
            itemType, workspaceId, itemId);
        return Task.CompletedTask;
    }

    public async Task<StoredItemDefinition?> GetAsync(
        string itemType,
        Guid workspaceId,
        Guid itemId,
        CancellationToken ct = default)
    {
        var key = CacheKey(itemType, workspaceId, itemId);
        if (_options.CacheTtlSeconds > 0 && _cache.TryGetValue(key, out StoredItemDefinition cached))
        {
            return cached;
        }

        var url = $"{_options.FabricApiBaseUrl}/workspaces/{workspaceId}/items/{itemId}/getDefinition";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        await AuthorizeAsync(request, ct).ConfigureAwait(false);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        // getDefinition almost always returns 202 + Location header (LRO).
        var payload = await CompleteLongRunningAsync(response, ct).ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        var envelope = ParseDefinitionResponse(payload);
        if (envelope is null)
        {
            return null;
        }

        // Fabric doesn't return a usable strong ETag for item definitions, so
        // synthesise one from the content. Stable across reads (so unchanged
        // content keeps the same tag) and changes whenever any part bytes change.
        var etag = ComputeContentETag(envelope);
        var stored = new StoredItemDefinition(envelope, etag);

        if (_options.CacheTtlSeconds > 0)
        {
            _cache.Set(key, stored, TimeSpan.FromSeconds(_options.CacheTtlSeconds));
        }
        return stored;
    }

    public Task DeleteAsync(string itemType, Guid workspaceId, Guid itemId, bool hard, CancellationToken ct = default)
    {
        // Fabric owns the lifecycle. The lifecycle controller receives a
        // Delete notification; we only need to drop our cached copy so the
        // next GetAsync hits the (now-empty) source of truth.
        _cache.Remove(CacheKey(itemType, workspaceId, itemId));
        _logger.LogInformation(
            "Cache evicted for {ItemType}/{Workspace}/{Item} (Fabric-side delete, hard={Hard})",
            itemType, workspaceId, itemId, hard);
        return Task.CompletedTask;
    }

    public Task RestoreAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope? envelope, CancellationToken ct = default)
    {
        // Same rationale as DeleteAsync — Fabric is authoritative.
        _cache.Remove(CacheKey(itemType, workspaceId, itemId));
        return Task.CompletedTask;
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _tokenProvider.GetTokenAsync(AuthenticationHelper.FabricItemsApiScopes, ct).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// If <paramref name="initial"/> is 202, polls the <c>Location</c> URL
    /// until completion (or timeout) and returns the final body bytes (or
    /// null if the LRO returns no body). If the initial response is already
    /// terminal, returns its body directly.
    /// </summary>
    private async Task<byte[]?> CompleteLongRunningAsync(HttpResponseMessage initial, CancellationToken ct)
    {
        if (initial.StatusCode == HttpStatusCode.Accepted && initial.Headers.Location is { } location)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(_options.LongRunningTimeoutSeconds);
            var pollUrl = location.IsAbsoluteUri ? location.ToString() : $"{_options.FabricApiBaseUrl.TrimEnd('/')}/{location.OriginalString.TrimStart('/')}";

            while (true)
            {
                if (DateTimeOffset.UtcNow > deadline)
                {
                    throw new TimeoutException($"Fabric long-running operation did not complete within {_options.LongRunningTimeoutSeconds}s: {pollUrl}");
                }

                var delay = initial.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(_options.LongRunningPollSeconds);
                await Task.Delay(delay, ct).ConfigureAwait(false);

                using var poll = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                await AuthorizeAsync(poll, ct).ConfigureAwait(false);
                using var pollResponse = await _http.SendAsync(poll, ct).ConfigureAwait(false);

                // Per Fabric LRO conventions the operation endpoint returns
                // 202 while running, then either redirects to a result URL or
                // returns the result body directly with 200.
                if (pollResponse.StatusCode == HttpStatusCode.Accepted)
                {
                    continue;
                }

                await EnsureSuccessAsync(pollResponse, ct).ConfigureAwait(false);

                // Some endpoints expose the result via a separate "result" URL
                // on the operation; follow it if the body is empty.
                var pollBody = await pollResponse.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (pollBody.Length > 0)
                {
                    return pollBody;
                }

                if (pollResponse.Headers.TryGetValues("Location", out var resultLocations))
                {
                    var resultUrl = resultLocations.First();
                    using var result = new HttpRequestMessage(HttpMethod.Get, resultUrl);
                    await AuthorizeAsync(result, ct).ConfigureAwait(false);
                    using var resultResponse = await _http.SendAsync(result, ct).ConfigureAwait(false);
                    await EnsureSuccessAsync(resultResponse, ct).ConfigureAwait(false);
                    return await resultResponse.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                }

                return null;
            }
        }

        await EnsureSuccessAsync(initial, ct).ConfigureAwait(false);
        return await initial.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        throw new HttpRequestException(
            $"Fabric API call to {response.RequestMessage?.RequestUri} failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}",
            null,
            response.StatusCode);
    }

    private static ItemDefinitionEnvelope? ParseDefinitionResponse(byte[] body)
    {
        if (body.Length == 0) return null;
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("definition", out var defEl)) return null;
        if (!defEl.TryGetProperty("parts", out var partsEl) || partsEl.ValueKind != JsonValueKind.Array) return null;

        var parts = new List<ItemDefinitionPart>(partsEl.GetArrayLength());
        foreach (var partEl in partsEl.EnumerateArray())
        {
            parts.Add(new ItemDefinitionPart
            {
                Path = partEl.TryGetProperty("path", out var p) ? p.GetString() ?? string.Empty : string.Empty,
                Payload = partEl.TryGetProperty("payload", out var pl) ? pl.GetString() ?? string.Empty : string.Empty,
                PayloadType = partEl.TryGetProperty("payloadType", out var pt) ? pt.GetString() ?? "InlineBase64" : "InlineBase64"
            });
        }

        return new ItemDefinitionEnvelope
        {
            Definition = new ItemDefinition { Parts = parts }
        };
    }

    private static string ComputeContentETag(ItemDefinitionEnvelope envelope)
    {
        // SHA-256 over a stable ordering of (path, payloadType, payload) so
        // that two reads of the same OneLake state produce the same ETag.
        var sb = new StringBuilder();
        if (envelope.Definition?.Parts is { } parts)
        {
            foreach (var part in parts.OrderBy(p => p.Path, StringComparer.Ordinal))
            {
                sb.Append(part.Path).Append('\n')
                  .Append(part.PayloadType).Append('\n')
                  .Append(part.Payload).Append('\n');
            }
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash);
    }
}
