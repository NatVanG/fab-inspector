using FabInspector.Web.Workload;
using FabInspector.Web.Workload.Auth;
using FabInspector.Web.Workload.Jobs;
using FabInspector.Web.Workload.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabInspector.Web.Controllers;

/// <summary>
/// Implements the Fabric Item Job action endpoints (start / status / cancel)
/// that Fabric calls when a user runs a workload item — either from the
/// custom <c>fabinspector.run.*</c> context menu actions or from the
/// Monitor Hub.
/// </summary>
[ApiController]
[Route("api/workload/jobs/{itemType}/{workspaceId:guid}/{itemId:guid}/{jobType}/{jobInstanceId:guid}")]
[Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName)]
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
    public IActionResult Start(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId)
    {
        if (!WorkloadItemTypes.IsKnown(itemType))
            return BadRequest(new { error = $"Unknown item type '{itemType}'." });

        if (_runs.Get(jobInstanceId) != null)
            return Conflict(new { error = $"Job {jobInstanceId} already exists." });

        var record = _runs.Create(itemType, workspaceId, itemId, jobType, jobInstanceId);

        // Fire and forget; status is polled via GET.
        _ = Task.Run(() => _runner.RunJobAsync(record));

        return Accepted(new { jobInstanceId, status = record.Status.ToString() });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetStatus(Guid jobInstanceId)
    {
        var record = _runs.Get(jobInstanceId);
        if (record == null) return NotFound();

        return Ok(new
        {
            jobInstanceId = record.JobInstanceId,
            itemType = record.ItemType,
            workspaceId = record.WorkspaceId,
            itemId = record.ItemId,
            jobType = record.JobType,
            status = record.Status.ToString(),
            startTime = record.StartTime,
            endTime = record.EndTime,
            failureMessage = record.FailureMessage,
            passCount = record.PassCount,
            failCount = record.FailCount,
            log = record.Log
        });
    }

    [HttpPost("cancel")]
    public IActionResult Cancel(Guid jobInstanceId)
    {
        var record = _runs.Get(jobInstanceId);
        if (record == null) return NotFound();

        try { record.Cts.Cancel(); } catch (ObjectDisposedException) { }
        return Ok(new { jobInstanceId, status = record.Status.ToString() });
    }
}
