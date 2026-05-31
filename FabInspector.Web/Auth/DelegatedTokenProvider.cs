using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using FabInspector.Core;
using FabInspector.Web.Workload.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace FabInspector.Web.Auth;

/// <summary>
/// <see cref="ITokenProvider"/> implementation that acquires delegated user
/// tokens for downstream Power BI / Fabric / OneLake calls. When the request
/// arrived via the Fabric workload <c>SubjectAndAppToken1.0</c> scheme, the
/// embedded subject token is exchanged via the On-Behalf-Of flow; otherwise
/// (interactive OIDC sign-in) the cached user token is used.
/// </summary>
public sealed class DelegatedTokenProvider : ITokenProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DelegatedTokenProvider(ITokenAcquisition tokenAcquisition, IHttpContextAccessor httpContextAccessor)
    {
        _tokenAcquisition = tokenAcquisition;
        _httpContextAccessor = httpContextAccessor;
        Credential = new DelegatedTokenCredential(GetTokenAsync);
    }

    public TokenCredential Credential { get; }

    public async Task<string> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var subjectToken = httpContext?.Items[SubjectAndAppTokenAuthHandler.SubjectTokenContextKey] as string;

        try
        {
            if (!string.IsNullOrEmpty(subjectToken))
            {
                // On-Behalf-Of flow: exchange the Fabric-supplied subject token
                // for a downstream token. Microsoft.Identity.Web bridges this
                // through ITokenAcquisition when the incoming bearer is the
                // user assertion.
                var options = new TokenAcquisitionOptions
                {
                    CancellationToken = cancellationToken
                };
                return await _tokenAcquisition
                    .GetAccessTokenForUserAsync(scopes, tokenAcquisitionOptions: options, user: httpContext?.User)
                    .ConfigureAwait(false);
            }

            return await _tokenAcquisition
                .GetAccessTokenForUserAsync(scopes, user: httpContext?.User)
                .ConfigureAwait(false);
        }
        catch (MsalUiRequiredException ex)
        {
            // The user has not consented to one of the downstream scopes
            // (e.g. Fabric/OneLake). Surface as a typed exception so the
            // exception filter can return a structured ErrorResponse with
            // an AAD claims challenge for the React UI to re-prompt.
            throw new ConsentRequiredException(
                $"Consent required for scopes: {string.Join(", ", scopes)}. {ex.Message}",
                claimsChallenge: ex.Claims,
                scopes: scopes,
                inner: ex);
        }
        catch (MicrosoftIdentityWebChallengeUserException ex) when (ex.MsalUiRequiredException != null)
        {
            throw new ConsentRequiredException(
                $"Consent required for scopes: {string.Join(", ", scopes)}. {ex.MsalUiRequiredException.Message}",
                claimsChallenge: ex.MsalUiRequiredException.Claims,
                scopes: scopes,
                inner: ex);
        }
    }
}
