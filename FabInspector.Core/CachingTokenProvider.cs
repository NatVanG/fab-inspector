using Azure.Core;
using System.Collections.Concurrent;

namespace FabInspector.Core
{
    /// <summary>
    /// Thread-safe, multi-scope token provider that caches tokens per scope set
    /// and refreshes them with a 5-minute buffer before expiry.
    /// 
    /// Replaces the duplicated EnsureAuthenticatedAsync patterns formerly in
    /// Main.cs and FabricRemoteFileSystem.
    /// </summary>
    public sealed class CachingTokenProvider : ITokenProvider
    {
        private readonly TokenCredential _credential;
        private readonly ConcurrentDictionary<string, ScopeCacheEntry> _cache = new(StringComparer.Ordinal);

        public CachingTokenProvider(TokenCredential credential)
        {
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        }

        public TokenCredential Credential => _credential;

        public async Task<string> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
        {
            var cacheKey = string.Join("|", scopes);
            var entry = _cache.GetOrAdd(cacheKey, _ => new ScopeCacheEntry());

            // Fast path: token is valid with 5-minute buffer
            if (entry.IsValid)
            {
                return entry.Token.Token;
            }

            await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (entry.IsValid)
                {
                    return entry.Token.Token;
                }

                var tokenRequestContext = new TokenRequestContext(scopes);
                var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken).ConfigureAwait(false);
                entry.Token = accessToken;
                entry.Initialized = true;

                return accessToken.Token;
            }
            finally
            {
                entry.Semaphore.Release();
            }
        }

        private sealed class ScopeCacheEntry
        {
            public readonly SemaphoreSlim Semaphore = new(1, 1);
            public AccessToken Token;
            public bool Initialized;

            public bool IsValid => Initialized && Token.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5);
        }
    }
}
