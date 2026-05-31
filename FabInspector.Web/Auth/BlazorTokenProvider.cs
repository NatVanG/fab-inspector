using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using FabInspector.Core;
using Microsoft.Identity.Web;

namespace FabInspector.Web.Auth;

/// <summary>
/// <see cref="ITokenProvider"/> implementation that acquires delegated user tokens
/// via <see cref="ITokenAcquisition"/> (Microsoft.Identity.Web) for the signed-in
/// Blazor user. Safe to construct per request — wraps cached token acquisition.
/// </summary>
public sealed class BlazorTokenProvider : ITokenProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;

    public BlazorTokenProvider(ITokenAcquisition tokenAcquisition)
    {
        _tokenAcquisition = tokenAcquisition;
        Credential = new DelegatedTokenCredential(GetTokenAsync);
    }

    public TokenCredential Credential { get; }

    public Task<string> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        // Microsoft.Identity.Web expects scopes without the "/.default" suffix when
        // the user has consented to specific scopes, but accepts ".default" too for
        // pre-consented apps. We pass through as-is to match the rest of the codebase.
        return _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
    }
}
