using System.Collections.Concurrent;

namespace FabInspector.Web.Workload.Jobs;

public enum JobStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public sealed class JobRunRecord
{
    public required Guid JobInstanceId { get; init; }
    public required string ItemType { get; init; }
    public required Guid WorkspaceId { get; init; }
    public required Guid ItemId { get; init; }
    public required string JobType { get; init; }

    public JobStatus Status { get; set; } = JobStatus.NotStarted;
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; set; }
    public string? FailureMessage { get; set; }
    public int PassCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Log { get; } = new();

    public CancellationTokenSource Cts { get; } = new();
}

public interface IJobRunStore
{
    JobRunRecord Create(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId);
    JobRunRecord? Get(Guid jobInstanceId);
    IReadOnlyCollection<JobRunRecord> List(Guid workspaceId, Guid itemId);
}

internal sealed class InMemoryJobRunStore : IJobRunStore
{
    private readonly ConcurrentDictionary<Guid, JobRunRecord> _runs = new();

    public JobRunRecord Create(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId)
    {
        var record = new JobRunRecord
        {
            JobInstanceId = jobInstanceId,
            ItemType = itemType,
            WorkspaceId = workspaceId,
            ItemId = itemId,
            JobType = jobType
        };
        _runs[jobInstanceId] = record;
        return record;
    }

    public JobRunRecord? Get(Guid jobInstanceId) =>
        _runs.TryGetValue(jobInstanceId, out var r) ? r : null;

    public IReadOnlyCollection<JobRunRecord> List(Guid workspaceId, Guid itemId) =>
        _runs.Values
            .Where(r => r.WorkspaceId == workspaceId && r.ItemId == itemId)
            .OrderByDescending(r => r.StartTime)
            .ToList();
}
