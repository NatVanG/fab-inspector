namespace FabInspector.Web.Workload.Auth;

/// <summary>
/// Thrown when an On-Behalf-Of token exchange (or any downstream MSAL call)
/// fails because the user has not consented to the requested scope, or AAD
/// returned a <c>claims</c> challenge that must be re-issued interactively.
/// The middleware maps this to an HTTP <c>403 Forbidden</c> with an
/// <c>ErrorResponse</c> of <c>errorCode = ConsentRequired</c> and — when
/// available — a <c>WWW-Authenticate: Bearer claims=...</c> header so the
/// React workload UI can re-prompt for consent.
/// </summary>
public sealed class ConsentRequiredException : Exception
{
    public ConsentRequiredException(string message, string? claimsChallenge = null, string[]? scopes = null, Exception? inner = null)
        : base(message, inner)
    {
        ClaimsChallenge = claimsChallenge;
        Scopes = scopes ?? Array.Empty<string>();
    }

    public string? ClaimsChallenge { get; }
    public string[] Scopes { get; }
}
