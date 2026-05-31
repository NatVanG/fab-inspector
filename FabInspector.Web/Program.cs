using FabInspector.Core;
using FabInspector.Web.Auth;
using FabInspector.Web.Components;
using FabInspector.Web.Services;
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

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddInMemoryTokenCaches();

// Fabric Extensibility Toolkit lifecycle endpoints authenticate via the
// SubjectAndAppToken1.0 scheme (delegated subject token + app-only token).
// Registered as a *second* scheme so the Blazor UI continues to use OIDC.
builder.Services
    .AddAuthentication()
    .AddScheme<SubjectAndAppTokenOptions, SubjectAndAppTokenAuthHandler>(
        SubjectAndAppTokenAuthHandler.SchemeName,
        options =>
        {
            // Local dev shortcut: allow unauthenticated calls to the workload
            // endpoints before AAD is registered. Production deployments must
            // override this in appsettings.
            options.AllowAnonymousInDevelopment = builder.Environment.IsDevelopment();
        });

builder.Services.AddAuthorization(options =>
{
    // Require an authenticated user on every page by default.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// Blazor needs cascading auth state for <AuthorizeView> / <AuthorizeRouteView>.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// Per-request token provider that wraps Microsoft.Identity.Web's ITokenAcquisition.
builder.Services.AddScoped<ITokenProvider, BlazorTokenProvider>();

// Operator catalogue + report renderer (singletons; pure compute, no per-user state).
builder.Services.AddFabInspectorOperators();

// Inspection orchestration — scoped so each Blazor circuit gets a runner bound to
// its own ITokenProvider, even though the underlying engine serialises runs.
builder.Services.AddScoped<InspectionRunner>();

// Custom Fabric workload item infrastructure (rule set + rules catalog).
// Stores are singletons — they back the in-process cache of item definitions
// and job-run records. WorkloadInspectionService and ItemDefinitionResolver are
// scoped so they pick up the request's InspectionRunner.
builder.Services.AddSingleton<IItemDefinitionStore, InMemoryItemDefinitionStore>();
builder.Services.AddSingleton<IJobRunStore, InMemoryJobRunStore>();
builder.Services.AddScoped<ItemDefinitionResolver>();
builder.Services.AddScoped<WorkloadInspectionService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Allow the Fabric portal to embed the workload-launch page in an iframe.
// Browsers honour CSP frame-ancestors and ignore X-Frame-Options when CSP is
// present, so we both set frame-ancestors and strip any default XFO header on
// the /fabric-launch route.
app.Use(async (ctx, next) =>
{
    // /fabric-launch covers both the legacy landing page and the new editor
    // routes /fabric-launch/ruleset and /fabric-launch/catalog via StartsWithSegments.
    if (ctx.Request.Path.StartsWithSegments("/fabric-launch"))
    {
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers["Content-Security-Policy"] =
                "frame-ancestors 'self' https://app.fabric.microsoft.com https://*.fabric.microsoft.com https://app.powerbi.com";
            ctx.Response.Headers.Remove("X-Frame-Options");
            return Task.CompletedTask;
        });
    }
    await next();
});

app.UseAntiforgery();

app.MapControllers(); // exposes /MicrosoftIdentity/Account/SignIn|SignOut
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
