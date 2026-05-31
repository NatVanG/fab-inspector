using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FabInspector.Web.Workload.Auth;

public sealed class SubjectAndAppTokenOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// When true, requests with no or malformed <c>Authorization</c> header are
    /// accepted (a synthetic anonymous principal is created). Intended for
    /// local development before AAD is registered. Should be bound strictly to
    /// <see cref="IHostEnvironment.IsDevelopment"/>.
    /// </summary>
    public bool AllowAnonymousInDevelopment { get; set; }

    /// <summary>The AAD app id of THIS workload (audience for incoming tokens).</summary>
    public string? WorkloadAppId { get; set; }

    /// <summary>
    /// The AAD app id of the Fabric service that mints app tokens.
    /// Defaults to the published Fabric appId <c>871c010f-5e61-4fb1-83ac-98610a7e9110</c>;
    /// override for sovereign clouds.
    /// </summary>
    public string FabricAppId { get; set; } = "871c010f-5e61-4fb1-83ac-98610a7e9110";

    /// <summary>
    /// OIDC authority used to discover JWKS. Defaults to
    /// <c>https://login.microsoftonline.com/common/v2.0</c>; override per
    /// tenant for B2B/single-tenant deployments.
    /// </summary>
    public string Authority { get; set; } = "https://login.microsoftonline.com/common/v2.0";

    /// <summary>
    /// If non-empty, incoming tokens whose <c>tid</c> is not in this list are
    /// rejected. Leave empty to accept any tenant (multi-tenant workload).
    /// </summary>
    public string[] AllowedTenants { get; set; } = Array.Empty<string>();

    /// <summary>The scope the subject token must contain to be accepted as a Fabric workload-control call.</summary>
    public string RequiredSubjectScope { get; set; } = "FabricWorkloadControl";
}

/// <summary>
/// Parses and cryptographically validates the Fabric Extensibility Toolkit
/// <c>SubjectAndAppToken1.0</c> authorization scheme. Each call must include
/// an <c>appToken</c> (proves the call came from Fabric) and may include a
/// <c>subjectToken</c> (the end-user identity, used for OBO).
/// </summary>
internal sealed class SubjectAndAppTokenAuthHandler : AuthenticationHandler<SubjectAndAppTokenOptions>
{
    public const string SchemeName = "SubjectAndAppToken1.0";
    public const string SubjectTokenContextKey = "FabInspector.Workload.SubjectToken";
    public const string AppTokenContextKey = "FabInspector.Workload.AppToken";

    private static readonly JwtSecurityTokenHandler JwtHandler = new() { MapInboundClaims = false };

    // Lazily-initialised, per-authority JWKS configuration manager. Cached across
    // requests so JWKS keys are only fetched once per refresh interval.
    private static readonly Lazy<Dictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>> _configManagers =
        new(() => new(StringComparer.OrdinalIgnoreCase));

    public SubjectAndAppTokenAuthHandler(
        IOptionsMonitor<SubjectAndAppTokenOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return Options.AllowAnonymousInDevelopment
                ? SuccessAnonymous()
                : AuthenticateResult.NoResult();
        }

        const string prefix = SchemeName + " ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail($"Authorization scheme is not '{SchemeName}'.");
        }

        var payload = header.Substring(prefix.Length);
        var (subjectToken, appToken) = ParseTokens(payload);

        if (string.IsNullOrEmpty(appToken))
        {
            return AuthenticateResult.Fail("appToken is required.");
        }

        if (string.IsNullOrEmpty(Options.WorkloadAppId))
        {
            // Mis-configuration: refuse to authenticate to avoid accidentally
            // accepting unsigned tokens in production. Dev mode is handled
            // above via the early return.
            return AuthenticateResult.Fail("Workload:Auth:WorkloadAppId is not configured.");
        }

        SigningKeys signingKeys;
        try
        {
            signingKeys = await GetSigningKeysAsync(Options.Authority).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to fetch JWKS from {Authority}", Options.Authority);
            return AuthenticateResult.Fail("Unable to fetch token signing keys.");
        }

        // --- Validate appToken: must be app-only, from Fabric ---
        ClaimsPrincipal appPrincipal;
        try
        {
            appPrincipal = ValidateToken(appToken, signingKeys, requireIdtypApp: true, requireScope: null, out _);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "appToken validation failed");
            return AuthenticateResult.Fail($"appToken validation failed: {ex.Message}");
        }

        var appAppId = appPrincipal.FindFirst("appid")?.Value ?? appPrincipal.FindFirst("azp")?.Value;
        if (!string.Equals(appAppId, Options.FabricAppId, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail($"appToken 'appid' is '{appAppId}', expected Fabric appId '{Options.FabricAppId}'.");
        }
        if (Options.AllowedTenants.Length > 0)
        {
            var tid = appPrincipal.FindFirst("tid")?.Value;
            if (tid == null || !Options.AllowedTenants.Contains(tid, StringComparer.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Fail($"appToken tenant '{tid}' is not in AllowedTenants.");
            }
        }

        // --- Validate subjectToken (if present) ---
        ClaimsPrincipal? subjectPrincipal = null;
        if (!string.IsNullOrEmpty(subjectToken))
        {
            try
            {
                subjectPrincipal = ValidateToken(
                    subjectToken,
                    signingKeys,
                    requireIdtypApp: false,
                    requireScope: Options.RequiredSubjectScope,
                    out _);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "subjectToken validation failed");
                return AuthenticateResult.Fail($"subjectToken validation failed: {ex.Message}");
            }

            var subjectAppId = subjectPrincipal.FindFirst("appid")?.Value ?? subjectPrincipal.FindFirst("azp")?.Value;
            if (!string.Equals(subjectAppId, appAppId, StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.Fail("subjectToken 'appid' does not match appToken 'appid'.");
            }
        }

        Context.Items[SubjectTokenContextKey] = subjectToken;
        Context.Items[AppTokenContextKey] = appToken;

        // Prefer the subject identity (real user); fall back to app principal for
        // app-only invocations (e.g. scheduled jobs without an end-user context).
        var principal = subjectPrincipal ?? appPrincipal;
        // Tag the principal with the authentication scheme name so downstream
        // [Authorize(AuthenticationSchemes=...)] checks can disambiguate.
        var identity = (ClaimsIdentity)principal.Identity!;
        if (!identity.HasClaim(ClaimTypes.AuthenticationMethod, SchemeName))
        {
            identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, SchemeName));
        }
        identity.AddClaim(new Claim("appTokenPresent", "true"));
        identity.AddClaim(new Claim("subjectTokenPresent", subjectPrincipal != null ? "true" : "false"));

        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private ClaimsPrincipal ValidateToken(string token, SigningKeys signingKeys, bool requireIdtypApp, string? requireScope, out JwtSecurityToken validated)
    {
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = signingKeys.ValidIssuers,
            ValidateAudience = true,
            ValidAudiences = new[] { Options.WorkloadAppId!, $"api://{Options.WorkloadAppId}" },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = signingKeys.Keys,
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };

        var principal = JwtHandler.ValidateToken(token, parameters, out var securityToken);
        validated = (JwtSecurityToken)securityToken;

        var idtyp = principal.FindFirst("idtyp")?.Value;
        var scopes = principal.FindFirst("scp")?.Value;

        if (requireIdtypApp)
        {
            // App-only tokens must have idtyp=app AND no scp claim.
            if (!string.Equals(idtyp, "app", StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityTokenValidationException($"Expected idtyp='app', got '{idtyp ?? "<none>"}'.");
            }
            if (!string.IsNullOrEmpty(scopes))
            {
                throw new SecurityTokenValidationException("App-only token must not contain a 'scp' claim.");
            }
        }
        else
        {
            // Delegated subject tokens must NOT carry idtyp=app.
            if (string.Equals(idtyp, "app", StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityTokenValidationException("Subject token must not be app-only (idtyp=app).");
            }
            if (!string.IsNullOrEmpty(requireScope))
            {
                var scopeList = (scopes ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (!scopeList.Contains(requireScope, StringComparer.Ordinal))
                {
                    throw new SecurityTokenValidationException($"Subject token must contain scope '{requireScope}'.");
                }
            }
        }

        return principal;
    }

    private static async Task<SigningKeys> GetSigningKeysAsync(string authority)
    {
        var managers = _configManagers.Value;
        ConfigurationManager<OpenIdConnectConfiguration> manager;
        lock (managers)
        {
            if (!managers.TryGetValue(authority, out manager!))
            {
                manager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever { RequireHttps = true })
                {
                    AutomaticRefreshInterval = TimeSpan.FromHours(12),
                    RefreshInterval = TimeSpan.FromMinutes(5)
                };
                managers[authority] = manager;
            }
        }

        var config = await manager.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false);
        return new SigningKeys(config.SigningKeys, new[] { config.Issuer });
    }

    private AuthenticateResult SuccessAnonymous()
    {
        Logger.LogWarning("SubjectAndAppToken handler is accepting an anonymous request (AllowAnonymousInDevelopment=true). This must not happen outside Development.");
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

    private sealed record SigningKeys(ICollection<SecurityKey> Keys, string[] ValidIssuers);
}
