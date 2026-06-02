using System.Collections.Concurrent;

namespace FabInspector.Web.Workload.Jobs;

public enum JobStatus
{
    NotStarted,
    InProgress,
    // Renamed from "Completed" to match the Fabric Workload Jobs Swagger
    // (status enum: NotStarted | InProgress | Succeeded | Failed | Cancelled).
    Succeeded,
    Failed,
    Cancelled
}

public sealed class JobErrorDetails
{
    public string ErrorCode { get; set; } = "Internal";
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = "FabInspector";
    public bool IsPermanent { get; set; }
}

public sealed class JobRunRecord
{
    public required Guid JobInstanceId { get; init; }
    public required string ItemType { get; init; }
    public required Guid WorkspaceId { get; init; }
    public required Guid ItemId { get; init; }
    public required string JobType { get; init; }

    public JobStatus Status { get; set; } = JobStatus.NotStarted;
    public DateTimeOffset StartTimeUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTimeUtc { get; set; }
    public JobErrorDetails? ErrorDetails { get; set; }
    public int PassCount { get; set; }
    public int FailCount { get; set; }
    public List<string> Log { get; } = new();

    public CancellationTokenSource Cts { get; } = new();
}

public interface IJobRunStore
{
    /// <summary>Create a new run record and persist its initial state.</summary>
    Task<JobRunRecord> CreateAsync(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId, CancellationToken ct = default);

    /// <summary>
    /// Resolve a run record by id. Implementations are expected to return the
    /// same in-process instance for the lifetime of the live job so callers can
    /// continue to share a <see cref="CancellationTokenSource"/>; once the job
    /// has terminated and been evicted from the process cache, durable stores
    /// rehydrate a fresh record from storage.
    /// </summary>
    Task<JobRunRecord?> GetAsync(Guid jobInstanceId, CancellationToken ct = default);

    Task<IReadOnlyCollection<JobRunRecord>> ListAsync(Guid workspaceId, Guid itemId, CancellationToken ct = default);

    /// <summary>
    /// Flush the current state of <paramref name="record"/> to the backing
    /// store. Callers should invoke this whenever a status transition is
    /// observable to external consumers (on start, on terminal status, on
    /// cancel). For in-memory stores this is a no-op.
    /// </summary>
    Task SaveAsync(JobRunRecord record, CancellationToken ct = default);
}

public sealed class InMemoryJobRunStore : IJobRunStore
{
    private readonly ConcurrentDictionary<Guid, JobRunRecord> _runs = new();

    public Task<JobRunRecord> CreateAsync(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId, CancellationToken ct = default)
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
        return Task.FromResult(record);
    }

    public Task<JobRunRecord?> GetAsync(Guid jobInstanceId, CancellationToken ct = default) =>
        Task.FromResult(_runs.TryGetValue(jobInstanceId, out var r) ? r : null);

    public Task<IReadOnlyCollection<JobRunRecord>> ListAsync(Guid workspaceId, Guid itemId, CancellationToken ct = default)
    {
        IReadOnlyCollection<JobRunRecord> list = _runs.Values
            .Where(r => r.WorkspaceId == workspaceId && r.ItemId == itemId)
            .OrderByDescending(r => r.StartTimeUtc)
            .ToList();
        return Task.FromResult(list);
    }

    // Mutations are made directly on the live record returned by GetAsync, so
    // there is nothing to flush for the in-memory backing.
    public Task SaveAsync(JobRunRecord record, CancellationToken ct = default) => Task.CompletedTask;
}
