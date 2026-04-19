using Azure.Core;

namespace FabInspector.Core
{
    /// <summary>
    /// Abstracts token acquisition so that a single caching implementation can serve
    /// every component that talks to Fabric, Power BI, or OneLake APIs.
    /// </summary>
    public interface ITokenProvider
    {
        /// <summary>
        /// Returns a valid bearer token string for the requested scopes.
        /// Implementations should cache tokens and refresh only when near expiry.
        /// </summary>
        Task<string> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exposes the underlying <see cref="TokenCredential"/> for SDKs (e.g. Azure.Storage.Files.DataLake)
        /// that require a credential object rather than a raw token string.
        /// </summary>
        TokenCredential Credential { get; }
    }
}
