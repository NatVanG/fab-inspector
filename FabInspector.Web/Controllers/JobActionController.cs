using FabInspector.Web.Workload;
using FabInspector.Web.Workload.Auth;
using FabInspector.Web.Workload.Contracts;
using FabInspector.Web.Workload.Jobs;
using FabInspector.Web.Workload.Runtime;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabInspector.Web.Controllers;

/// <summary>
/// Implements the Fabric Item Job action endpoints (start / status / cancel).
/// Route shape matches the Workload Jobs Swagger:
/// <c>.../jobs/{itemType}/{workspaceId}/{itemId}/{jobType}/jobInstances/{jobInstanceId}[/cancel]</c>.
/// Response shape uses the canonical fields <c>status</c>, <c>errorDetails</c>,
/// <c>startTimeUtc</c>, <c>endTimeUtc</c> with workload extras tucked under
/// a non-standard <c>fabInspector</c> envelope.
/// </summary>
[ApiController]
[Route("api/workload/jobs/{itemType}/{workspaceId:guid}/{itemId:guid}/{jobType}/jobInstances/{jobInstanceId:guid}")]
[Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName + "," + OpenIdConnectDefaults.AuthenticationScheme)]
public sealed class JobActionController : ControllerBase
{
    private readonly IJobRunStore _runs;
    private readonly WorkloadInspectionService _runner;
    private readonly ILogger<JobActionController> _logger;

    public JobActionController(IJobRunStore runs, WorkloadInspectionService runner, ILogger<JobActionController> logger)
    {
        _runs = runs;
        _runner = runner;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Start(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId, CancellationToken ct)
    {
        if (!WorkloadItemTypes.IsKnown(itemType))
            return BadRequest(new ErrorResponse("UnknownItemType", $"Unknown item type '{itemType}'."));

        if (jobType != WorkloadItemTypes.Jobs.RunRules && jobType != WorkloadItemTypes.Jobs.RunCatalog)
            return BadRequest(new ErrorResponse("UnknownJobType", $"Unknown job type '{jobType}'."));

        if (await _runs.GetAsync(jobInstanceId, ct) != null)
            return Conflict(new ErrorResponse("Conflict", $"Job instance {jobInstanceId} already exists."));

        var record = await _runs.CreateAsync(itemType, workspaceId, itemId, jobType, jobInstanceId, ct);

        // Fire and forget; status is polled via GET. The runner is responsible
        // for calling _runs.SaveAsync on every observable status transition.
        _ = Task.Run(() => _runner.RunJobAsync(record));

        return Accepted(BuildStatusResponse(record));
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus(Guid jobInstanceId, CancellationToken ct)
    {
        var record = await _runs.GetAsync(jobInstanceId, ct);
        if (record == null)
            return NotFound(new ErrorResponse("NotFound", $"Job instance {jobInstanceId} not found."));

        return Ok(BuildStatusResponse(record));
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(Guid jobInstanceId, CancellationToken ct)
    {
        var record = await _runs.GetAsync(jobInstanceId, ct);
        if (record == null)
            return NotFound(new ErrorResponse("NotFound", $"Job instance {jobInstanceId} not found."));

        try { record.Cts.Cancel(); } catch (ObjectDisposedException) { }
        // Persist the cancel intent so a second instance observing the
        // record from storage sees the in-progress cancel attempt even if
        // the runner has not yet flipped status to Cancelled.
        await _runs.SaveAsync(record, ct);
        return Ok(BuildStatusResponse(record));
    }

    private static object BuildStatusResponse(JobRunRecord record) => new
    {
        jobInstanceId = record.JobInstanceId,
        itemType = record.ItemType,
        workspaceId = record.WorkspaceId,
        itemId = record.ItemId,
        jobType = record.JobType,
        status = record.Status.ToString(),
        startTimeUtc = record.StartTimeUtc,
        endTimeUtc = record.EndTimeUtc,
        errorDetails = record.ErrorDetails == null ? null : new
        {
            errorCode = record.ErrorDetails.ErrorCode,
            message = record.ErrorDetails.Message,
            source = record.ErrorDetails.Source,
            isPermanent = record.ErrorDetails.IsPermanent
        },
        // Workload-specific extras carried in a non-standard envelope so they
        // don't conflict with Fabric's job-status contract.
        fabInspector = new
        {
            passCount = record.PassCount,
            failCount = record.FailCount,
            log = record.Log
        }
    };
}
