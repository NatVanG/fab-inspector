using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using FabInspector.Core;
using Microsoft.Identity.Web;

namespace FabInspector.Web.Auth;

/// <summary>
/// <see cref="ITokenProvider"/> implementation that acquires delegated user
/// tokens via <see cref="ITokenAcquisition"/> (Microsoft.Identity.Web).
/// Resolved per HTTP request; safe to compose with the in-memory token cache.
/// </summary>
public sealed class DelegatedTokenProvider : ITokenProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;

    public DelegatedTokenProvider(ITokenAcquisition tokenAcquisition)
    {
        _tokenAcquisition = tokenAcquisition;
        Credential = new DelegatedTokenCredential(GetTokenAsync);
    }

    public TokenCredential Credential { get; }

    public Task<string> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        return _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
    }
}
