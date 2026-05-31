using FabInspector.Web.Workload.Auth;
using FabInspector.Web.Workload.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FabInspector.Web.Workload;

/// <summary>
/// Global MVC filter that maps unhandled workload exceptions to the structured
/// <see cref="ErrorResponse"/> contract expected by the Fabric portal.
/// </summary>
internal sealed class WorkloadExceptionFilter : IExceptionFilter
{
    private readonly ILogger<WorkloadExceptionFilter> _logger;
    private readonly IHostEnvironment _env;

    public WorkloadExceptionFilter(ILogger<WorkloadExceptionFilter> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public void OnException(ExceptionContext context)
    {
        // Only apply to controllers under the workload route prefix; let
        // other handlers (e.g. the OIDC sign-in pages) bubble up unchanged.
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/workload/", StringComparison.OrdinalIgnoreCase)) return;

        switch (context.Exception)
        {
            case ConsentRequiredException cre:
                if (!string.IsNullOrEmpty(cre.ClaimsChallenge))
                {
                    context.HttpContext.Response.Headers["WWW-Authenticate"] =
                        $"Bearer claims=\"{cre.ClaimsChallenge}\"";
                }
                context.Result = new ObjectResult(new ErrorResponse(
                    errorCode: "ConsentRequired",
                    message: cre.Message,
                    source: "FabInspector.Workload.Auth",
                    isPermanent: false,
                    moreDetails: cre.Scopes.Length > 0 ? string.Join(',', cre.Scopes) : null))
                { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                return;

            default:
                _logger.LogError(context.Exception, "Unhandled workload exception on {Path}", path);
                context.Result = new ObjectResult(new ErrorResponse(
                    errorCode: "Internal",
                    message: _env.IsDevelopment()
                        ? context.Exception.Message
                        : "An internal error occurred.",
                    source: "FabInspector.Workload",
                    isPermanent: false))
                { StatusCode = StatusCodes.Status500InternalServerError };
                context.ExceptionHandled = true;
                return;
        }
    }
}
