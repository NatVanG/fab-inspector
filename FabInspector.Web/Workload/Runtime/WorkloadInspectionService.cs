using System.Text.Json;
using FabInspector.Core;
using FabInspector.Web.Services;
using FabInspector.Web.Workload.Jobs;

namespace FabInspector.Web.Workload.Runtime;

/// <summary>
/// Bridges a workload item-definition (rules or catalog) into a single
/// <see cref="InspectionRunner"/> invocation. The resolved rules JSON is
/// written to a per-run temp file so the existing path-based engine can
/// consume it without an in-memory overload.
/// </summary>
public sealed class WorkloadInspectionService
{
    private readonly InspectionRunner _runner;
    private readonly ItemDefinitionResolver _resolver;
    private readonly ILogger<WorkloadInspectionService> _logger;

    public WorkloadInspectionService(
        InspectionRunner runner,
        ItemDefinitionResolver resolver,
        ILogger<WorkloadInspectionService> logger)
    {
        _runner = runner;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task RunJobAsync(JobRunRecord record, CancellationToken externalCt = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(record.Cts.Token, externalCt);
        var ct = linked.Token;

        record.Status = JobStatus.InProgress;
        record.StartTimeUtc = DateTimeOffset.UtcNow;

        string? tempPath = null;
        try
        {
            InspectionRules? rules;
            if (record.JobType == WorkloadItemTypes.Jobs.RunRules)
            {
                rules = await _resolver.ResolveRuleSetAsync(record.WorkspaceId, record.ItemId, ct).ConfigureAwait(false);
                if (rules == null) throw new InvalidOperationException("Rule set item has no usable 'rules.json' payload.");
            }
            else if (record.JobType == WorkloadItemTypes.Jobs.RunCatalog)
            {
                rules = await _resolver.ResolveCatalogAsync(record.WorkspaceId, record.ItemId, record.Log, ct).ConfigureAwait(false);
                if (rules == null) throw new InvalidOperationException("Catalog item resolved to zero usable rules.");
            }
            else
            {
                throw new InvalidOperationException($"Unknown job type '{record.JobType}'.");
            }

            tempPath = Path.Combine(Path.GetTempPath(), $"fabinspector-{record.JobInstanceId:N}.json");
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(rules), ct).ConfigureAwait(false);

            var request = new InspectionRequest(
                FabricWorkspaceId: record.WorkspaceId.ToString(),
                FabricItemId: null,
                RulesFileUrl: tempPath,
                Verbose: true,
                Parallel: false,
                JsonOutput: true,
                HtmlOutput: false);

            var result = await _runner.RunAsync(request, progress: null, cancellationToken: ct).ConfigureAwait(false);

            if (result.Failure != null)
            {
                record.ErrorDetails = new JobErrorDetails
                {
                    ErrorCode = "InspectionFailed",
                    Message = result.Failure.Message,
                    Source = "FabInspector.Engine",
                    IsPermanent = true
                };
                record.Status = JobStatus.Failed;
            }
            else
            {
                var results = result.TestRun?.Results?.ToList() ?? new();
                record.PassCount = results.Count(r => r.Pass);
                record.FailCount = results.Count(r => !r.Pass);
                record.Status = JobStatus.Succeeded;
            }
            record.Log.AddRange(result.LogLines);
        }
        catch (OperationCanceledException)
        {
            record.Status = JobStatus.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workload inspection job {JobInstanceId} failed", record.JobInstanceId);
            record.ErrorDetails = new JobErrorDetails
            {
                ErrorCode = "Internal",
                Message = ex.Message,
                Source = "FabInspector.Workload",
                IsPermanent = false
            };
            record.Status = JobStatus.Failed;
        }
        finally
        {
            record.EndTimeUtc = DateTimeOffset.UtcNow;
            if (tempPath != null)
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }
}
