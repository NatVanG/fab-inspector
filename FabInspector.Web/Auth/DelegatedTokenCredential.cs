using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace FabInspector.Web.Auth;

/// <summary>
/// Adapter that turns a token-acquiring delegate into a <see cref="TokenCredential"/>
/// so SDKs (Azure.Storage.Files.DataLake, etc.) can use Blazor delegated user tokens.
/// </summary>
internal sealed class DelegatedTokenCredential : TokenCredential
{
    private readonly Func<string[], CancellationToken, Task<string>> _getToken;

    public DelegatedTokenCredential(Func<string[], CancellationToken, Task<string>> getToken)
    {
        _getToken = getToken;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var token = await _getToken(requestContext.Scopes, cancellationToken).ConfigureAwait(false);
        // Microsoft.Identity.Web caches tokens; we don't have direct access to the expiry
        // here, so report a conservative 5-minute validity. Callers re-acquire on every call
        // anyway (DataLake SDK caches its own token internally for the request duration).
        return new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(5));
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
}
