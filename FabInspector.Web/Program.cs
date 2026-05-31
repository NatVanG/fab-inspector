using FabInspector.Core;
using FabInspector.Web.Auth;
using FabInspector.Web.Services;
using FabInspector.Web.Workload;
using FabInspector.Web.Workload.Auth;
using FabInspector.Web.Workload.Jobs;
using FabInspector.Web.Workload.Runtime;
using FabInspector.Web.Workload.Stores;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// All Fabric / Power BI / OneLake scopes that the inspection runner may need to
// silently acquire after sign-in. Listed up front so MSAL can request them as
// initial scopes (and so consent is granted once).
var initialScopes = new[]
{
    "https://analysis.windows.net/powerbi/api/.default",
    "https://api.fabric.microsoft.com/.default",
    "https://storage.azure.com/.default"
};

// Interactive OIDC web-app sign-in is retained so the inspection runner can
// silently acquire downstream tokens (Power BI / Fabric / OneLake) via the
// MSAL cache. The React workload UI is hosted out-of-process (Vite dev server
// or static bundle) — it talks to this backend purely through /api/workload/*.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddInMemoryTokenCaches();

// Fabric Extensibility Toolkit lifecycle endpoints authenticate via the
// SubjectAndAppToken1.0 scheme (delegated subject token + app-only token).
// Registered as a *second* scheme so the OIDC web-app scheme stays available
// for interactive sign-in / sign-out controllers.
builder.Services
    .AddAuthentication()
    .AddScheme<SubjectAndAppTokenOptions, SubjectAndAppTokenAuthHandler>(
        SubjectAndAppTokenAuthHandler.SchemeName,
        options =>
        {
            // Cryptographic validation parameters. These MUST be configured in
            // production (Workload:Auth section in appsettings). The
            // AllowAnonymousInDevelopment escape hatch is strictly bound to
            // IsDevelopment() and is logged loudly on every hit so it can be
            // detected if it leaks into non-Dev environments.
            builder.Configuration.GetSection("Workload:Auth").Bind(options);
            options.AllowAnonymousInDevelopment =
                options.AllowAnonymousInDevelopment && builder.Environment.IsDevelopment();
        });

builder.Services.AddAuthorization(options =>
{
    // Controllers opt-in to a specific scheme via their own [Authorize]
    // attributes (workload endpoints require SubjectAndAppToken; the
    // MicrosoftIdentity sign-in controllers use OIDC). We still require an
    // authenticated user on anything that doesn't explicitly opt out.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllersWithViews(options =>
{
    // Map workload exceptions (e.g. ConsentRequiredException) to structured
    // ErrorResponse payloads expected by the Fabric portal.
    options.Filters.Add<WorkloadExceptionFilter>();
})
    .AddMicrosoftIdentityUI();

builder.Services.AddHttpContextAccessor();

// Per-request token provider that wraps Microsoft.Identity.Web's ITokenAcquisition.
builder.Services.AddScoped<ITokenProvider, DelegatedTokenProvider>();

// Operator catalogue + report renderer (singletons; pure compute, no per-user state).
builder.Services.AddFabInspectorOperators();

// Inspection orchestration — scoped per request so each invocation gets a
// runner bound to its own ITokenProvider, even though the underlying engine
// serialises runs.
builder.Services.AddScoped<InspectionRunner>();

// Custom Fabric workload item infrastructure (rule set + rules catalog).
// Stores are singletons — they back the in-process cache of item definitions
// and job-run records. WorkloadInspectionService and ItemDefinitionResolver are
// scoped so they pick up the request's InspectionRunner.
builder.Services.AddSingleton<IItemDefinitionStore, InMemoryItemDefinitionStore>();
builder.Services.AddSingleton<IJobRunStore, InMemoryJobRunStore>();
builder.Services.AddScoped<ItemDefinitionResolver>();
builder.Services.AddScoped<WorkloadInspectionService>();

// CORS for the React workload dev server and any configured production origin.
// In dev, Vite proxies /api/* through its own port, so the React app sees a
// same-origin request and this CORS policy is only exercised by tools that
// hit the backend directly (e.g. the DevGateway preview surface).
const string ReactCorsPolicy = "FabInspectorReactApp";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[] { "https://localhost:60006", "http://localhost:60006" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(ReactCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors(ReactCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); // exposes /api/workload/* and /MicrosoftIdentity/Account/SignIn|SignOut

app.Run();
