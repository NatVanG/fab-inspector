using System.Collections.Concurrent;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace FabInspector.Web.Workload.Jobs;

/// <summary>
/// Options bound from <c>Workload:Jobs</c>.
/// </summary>
public sealed class JobRunStoreOptions
{
    /// <summary>Which <see cref="IJobRunStore"/> implementation to register. <c>InMemory</c> or <c>AzureTable</c>.</summary>
    public string Store { get; set; } = "InMemory";

    /// <summary>Storage account connection string. Mutually exclusive with <see cref="AccountUri"/>.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Storage account Table service URI (e.g. <c>https://acme.table.core.windows.net</c>). Used with Managed Identity.</summary>
    public string? AccountUri { get; set; }

    /// <summary>Table name for job-run records. Created automatically on startup.</summary>
    public string RunsTableName { get; set; } = "FabInspectorJobRuns";

    /// <summary>Table name for chunked log lines. Created automatically on startup.</summary>
    public string LogsTableName { get; set; } = "FabInspectorJobLogs";

    /// <summary>Number of days after job start to retain rows. Older rows are deleted by the retention service.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often the retention background service runs (minutes).</summary>
    public int RetentionScanIntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Durable <see cref="IJobRunStore"/> backed by Azure Storage Tables. Live
/// records (those with active <see cref="System.Threading.CancellationTokenSource"/>s)
/// stay in a per-process dictionary so cancellation continues to work; on a
/// cache miss (process restart or other instance) the record is rehydrated
/// from the table without its CTS.
/// </summary>
internal sealed class AzureTableJobRunStore : IJobRunStore
{
    private readonly TableClient _runsTable;
    private readonly TableClient _logsTable;
    private readonly ILogger<AzureTableJobRunStore> _logger;

    // Process-local cache so the live job continues to share the same CTS with
    // the controller's cancel endpoint and the inspection service.
    private readonly ConcurrentDictionary<Guid, JobRunRecord> _live = new();

    public AzureTableJobRunStore(TableServiceClient serviceClient, IOptions<JobRunStoreOptions> options, ILogger<AzureTableJobRunStore> logger)
    {
        _logger = logger;
        _runsTable = serviceClient.GetTableClient(options.Value.RunsTableName);
        _logsTable = serviceClient.GetTableClient(options.Value.LogsTableName);
        // Idempotent — keeps deployments self-bootstrapping.
        _runsTable.CreateIfNotExists();
        _logsTable.CreateIfNotExists();
    }

    private static string PartitionKey(Guid workspaceId, Guid itemId) => $"{workspaceId:N}_{itemId:N}";
    private static string RowKey(Guid jobInstanceId) => jobInstanceId.ToString("N");

    public async Task<JobRunRecord> CreateAsync(string itemType, Guid workspaceId, Guid itemId, string jobType, Guid jobInstanceId, CancellationToken ct = default)
    {
        var record = new JobRunRecord
        {
            JobInstanceId = jobInstanceId,
            ItemType = itemType,
            WorkspaceId = workspaceId,
            ItemId = itemId,
            JobType = jobType
        };
        _live[jobInstanceId] = record;
        await UpsertEntityAsync(record, ct).ConfigureAwait(false);
        return record;
    }

    public async Task<JobRunRecord?> GetAsync(Guid jobInstanceId, CancellationToken ct = default)
    {
        if (_live.TryGetValue(jobInstanceId, out var live))
        {
            return live;
        }

        // Cache miss: scan by RowKey. PartitionKey is unknown here so we use a
        // RowKey filter — Tables service still returns quickly since RowKey is
        // indexed alongside PartitionKey. Job lookup happens infrequently
        // (only after a process restart or from another instance).
        var rowKey = RowKey(jobInstanceId);
        var rehydrated = await FindByRowKeyAsync(rowKey, ct).ConfigureAwait(false);
        return rehydrated;
    }

    public async Task<IReadOnlyCollection<JobRunRecord>> ListAsync(Guid workspaceId, Guid itemId, CancellationToken ct = default)
    {
        var pk = PartitionKey(workspaceId, itemId);
        var list = new List<JobRunRecord>();
        await foreach (var entity in _runsTable.QueryAsync<TableEntity>(e => e.PartitionKey == pk, cancellationToken: ct).ConfigureAwait(false))
        {
            list.Add(FromEntity(entity));
        }
        return list
            .OrderByDescending(r => r.StartTimeUtc)
            .ToList();
    }

    public async Task SaveAsync(JobRunRecord record, CancellationToken ct = default)
    {
        await UpsertEntityAsync(record, ct).ConfigureAwait(false);

        if (IsTerminal(record.Status))
        {
            // Once terminal, persist the log and forget the live CTS so the
            // record can be garbage-collected from the per-process cache.
            await WriteLogChunksAsync(record, ct).ConfigureAwait(false);
            _live.TryRemove(record.JobInstanceId, out _);
        }
    }

    private static bool IsTerminal(JobStatus s) => s is JobStatus.Succeeded or JobStatus.Failed or JobStatus.Cancelled;

    private async Task UpsertEntityAsync(JobRunRecord r, CancellationToken ct)
    {
        var entity = new TableEntity(PartitionKey(r.WorkspaceId, r.ItemId), RowKey(r.JobInstanceId))
        {
            ["ItemType"] = r.ItemType,
            ["WorkspaceId"] = r.WorkspaceId,
            ["ItemId"] = r.ItemId,
            ["JobType"] = r.JobType,
            ["Status"] = r.Status.ToString(),
            ["StartTimeUtc"] = r.StartTimeUtc,
            ["EndTimeUtc"] = r.EndTimeUtc,
            ["PassCount"] = r.PassCount,
            ["FailCount"] = r.FailCount,
            ["ErrorDetailsJson"] = r.ErrorDetails == null ? null : JsonSerializer.Serialize(r.ErrorDetails)
        };
        await _runsTable.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct).ConfigureAwait(false);
    }

    private async Task<JobRunRecord?> FindByRowKeyAsync(string rowKey, CancellationToken ct)
    {
        await foreach (var entity in _runsTable.QueryAsync<TableEntity>(e => e.RowKey == rowKey, maxPerPage: 1, cancellationToken: ct).ConfigureAwait(false))
        {
            var record = FromEntity(entity);
            await LoadLogAsync(record, ct).ConfigureAwait(false);
            return record;
        }
        return null;
    }

    private static JobRunRecord FromEntity(TableEntity e)
    {
        var record = new JobRunRecord
        {
            JobInstanceId = Guid.Parse(e.RowKey),
            ItemType = e.GetString("ItemType") ?? string.Empty,
            WorkspaceId = e.GetGuid("WorkspaceId") ?? Guid.Empty,
            ItemId = e.GetGuid("ItemId") ?? Guid.Empty,
            JobType = e.GetString("JobType") ?? string.Empty,
        };
        if (Enum.TryParse<JobStatus>(e.GetString("Status"), out var status)) record.Status = status;
        if (e.GetDateTimeOffset("StartTimeUtc") is { } start) record.StartTimeUtc = start;
        record.EndTimeUtc = e.GetDateTimeOffset("EndTimeUtc");
        record.PassCount = e.GetInt32("PassCount") ?? 0;
        record.FailCount = e.GetInt32("FailCount") ?? 0;
        var errorJson = e.GetString("ErrorDetailsJson");
        if (!string.IsNullOrEmpty(errorJson))
        {
            try { record.ErrorDetails = JsonSerializer.Deserialize<JobErrorDetails>(errorJson); }
            catch { /* tolerate forward-compat schema drift */ }
        }
        return record;
    }

    // Azure Table string properties are capped at 32 KiB per property. Long
    // job logs are chunked across multiple entities in JobLogs, ordered by
    // a zero-padded sequence in RowKey so reads concatenate in order.
    private const int ChunkSizeChars = 30 * 1024;

    private async Task WriteLogChunksAsync(JobRunRecord record, CancellationToken ct)
    {
        if (record.Log.Count == 0) return;

        var pk = record.JobInstanceId.ToString("N");
        var full = string.Join('\n', record.Log);
        var chunks = (full.Length + ChunkSizeChars - 1) / ChunkSizeChars;

        for (var i = 0; i < chunks; i++)
        {
            var start = i * ChunkSizeChars;
            var len = Math.Min(ChunkSizeChars, full.Length - start);
            var entity = new TableEntity(pk, i.ToString("D6"))
            {
                ["Chunk"] = full.Substring(start, len)
            };
            await _logsTable.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct).ConfigureAwait(false);
        }
    }

    private async Task LoadLogAsync(JobRunRecord record, CancellationToken ct)
    {
        var pk = record.JobInstanceId.ToString("N");
        var sb = new System.Text.StringBuilder();
        await foreach (var entity in _logsTable
            .QueryAsync<TableEntity>(e => e.PartitionKey == pk, cancellationToken: ct)
            .ConfigureAwait(false))
        {
            sb.Append(entity.GetString("Chunk") ?? string.Empty);
        }
        if (sb.Length > 0)
        {
            record.Log.AddRange(sb.ToString().Split('\n'));
        }
    }

    /// <summary>
    /// Delete run + log rows whose <c>StartTimeUtc</c> is older than the
    /// retention horizon. Called by <see cref="JobRunRetentionService"/>.
    /// </summary>
    internal async Task<int> EvictOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var deleted = 0;
        await foreach (var entity in _runsTable
            .QueryAsync<TableEntity>(e => e.GetDateTimeOffset("StartTimeUtc") < cutoff, cancellationToken: ct)
            .ConfigureAwait(false))
        {
            var jobId = entity.RowKey;
            // Best-effort: delete log chunks first so a partial failure
            // leaves the run row visible for a retry next cycle.
            await foreach (var logEntity in _logsTable
                .QueryAsync<TableEntity>(e => e.PartitionKey == jobId, cancellationToken: ct)
                .ConfigureAwait(false))
            {
                try { await _logsTable.DeleteEntityAsync(logEntity.PartitionKey, logEntity.RowKey, cancellationToken: ct).ConfigureAwait(false); }
                catch (RequestFailedException ex) when (ex.Status == 404) { /* concurrent delete */ }
            }
            try
            {
                await _runsTable.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken: ct).ConfigureAwait(false);
                deleted++;
            }
            catch (RequestFailedException ex) when (ex.Status == 404) { /* concurrent delete */ }
        }
        if (deleted > 0)
        {
            _logger.LogInformation("JobRunRetentionService evicted {Count} run records older than {Cutoff:o}", deleted, cutoff);
        }
        return deleted;
    }

    /// <summary>
    /// Build a <see cref="TableServiceClient"/> from <see cref="JobRunStoreOptions"/>:
    /// prefers <see cref="JobRunStoreOptions.ConnectionString"/> when set,
    /// otherwise authenticates to <see cref="JobRunStoreOptions.AccountUri"/>
    /// via <see cref="DefaultAzureCredential"/> (Managed Identity, VS, etc.).
    /// </summary>
    public static TableServiceClient BuildServiceClient(JobRunStoreOptions options)
    {
        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            return new TableServiceClient(options.ConnectionString);
        }
        if (!string.IsNullOrEmpty(options.AccountUri))
        {
            TokenCredential credential = new DefaultAzureCredential();
            return new TableServiceClient(new Uri(options.AccountUri), credential);
        }
        throw new InvalidOperationException(
            "AzureTable job-run store requires either Workload:Jobs:ConnectionString or Workload:Jobs:AccountUri.");
    }
}
