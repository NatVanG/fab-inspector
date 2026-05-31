using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FabInspector.Web.Workload.Auth;

public sealed class SubjectAndAppTokenOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// When true, requests with no or malformed <c>Authorization</c> header are
    /// accepted (a synthetic anonymous principal is created). Intended for local
    /// development before AAD is registered. Defaults to <c>false</c>.
    /// </summary>
    public bool AllowAnonymousInDevelopment { get; set; }
}

/// <summary>
/// Parses the Fabric Extensibility Toolkit <c>SubjectAndAppToken1.0</c>
/// authorization scheme. This handler does not cryptographically validate the
/// embedded tokens — AAD registration and signing-key validation are documented
/// as a separate, manual setup step. The handler does, however:
/// <list type="bullet">
/// <item>Extract the <c>subjectToken</c> and <c>appToken</c> from the header.</item>
/// <item>Stash the raw subject token in <see cref="HttpContext.Items"/> so
/// downstream code can use it for on-behalf-of exchange.</item>
/// <item>Produce a <see cref="ClaimsPrincipal"/> identifying the request as
/// having passed Fabric's app-to-app trust boundary.</item>
/// </list>
/// </summary>
internal sealed class SubjectAndAppTokenAuthHandler : AuthenticationHandler<SubjectAndAppTokenOptions>
{
    public const string SchemeName = "SubjectAndAppToken1.0";
    public const string SubjectTokenContextKey = "FabInspector.Workload.SubjectToken";
    public const string AppTokenContextKey = "FabInspector.Workload.AppToken";

    public SubjectAndAppTokenAuthHandler(
        IOptionsMonitor<SubjectAndAppTokenOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return Task.FromResult(Options.AllowAnonymousInDevelopment
                ? SuccessAnonymous()
                : AuthenticateResult.NoResult());
        }

        const string prefix = SchemeName + " ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Authorization scheme is not '{SchemeName}'."));
        }

        var payload = header.Substring(prefix.Length);
        var (subjectToken, appToken) = ParseTokens(payload);

        if (string.IsNullOrEmpty(appToken))
        {
            return Task.FromResult(AuthenticateResult.Fail("appToken is required."));
        }

        Context.Items[SubjectTokenContextKey] = subjectToken;
        Context.Items[AppTokenContextKey] = appToken;

        var claims = new List<Claim>
        {
            new(ClaimTypes.AuthenticationMethod, SchemeName),
            new("appTokenPresent", "true"),
            new("subjectTokenPresent", string.IsNullOrEmpty(subjectToken) ? "false" : "true")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private AuthenticateResult SuccessAnonymous()
    {
        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, SchemeName));
        identity.AddClaim(new Claim("dev", "true"));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private static (string? subjectToken, string? appToken) ParseTokens(string payload)
    {
        string? subject = null;
        string? app = null;
        foreach (var raw in payload.Split(','))
        {
            var part = raw.Trim();
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var key = part.Substring(0, eq).Trim();
            var value = part.Substring(eq + 1).Trim().Trim('"');
            if (key.Equals("subjectToken", StringComparison.OrdinalIgnoreCase)) subject = value;
            else if (key.Equals("appToken", StringComparison.OrdinalIgnoreCase)) app = value;
        }
        return (subject, app);
    }
}
