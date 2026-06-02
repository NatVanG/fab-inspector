using Microsoft.Extensions.Options;

namespace FabInspector.Web.Workload.Jobs;

/// <summary>
/// Hosted service that periodically evicts job-run records older than the
/// configured <see cref="JobRunStoreOptions.RetentionDays"/>. Active only
/// when the configured store is <c>AzureTable</c> (the in-memory store
/// has no persistent state worth retaining beyond a process lifetime).
/// </summary>
internal sealed class JobRunRetentionService : BackgroundService
{
    private readonly IJobRunStore _store;
    private readonly JobRunStoreOptions _options;
    private readonly ILogger<JobRunRetentionService> _logger;

    public JobRunRetentionService(
        IJobRunStore store,
        IOptions<JobRunStoreOptions> options,
        ILogger<JobRunRetentionService> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_store is not AzureTableJobRunStore azureStore)
        {
            // Eviction is a no-op for in-memory storage; exit early so we
            // don't keep a hosted timer alive for nothing.
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.RetentionScanIntervalMinutes));
        var retention = TimeSpan.FromDays(Math.Max(1, _options.RetentionDays));

        // Stagger the first run so multiple instances coming up together
        // don't all hit the table at the same moment.
        try { await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(30, 120)), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow - retention;
                await azureStore.EvictOlderThanAsync(cutoff, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Never crash the host because a single scan failed; log and try
                // again after the next interval.
                _logger.LogError(ex, "JobRunRetentionService scan failed; will retry in {Interval}", interval);
            }

            try { await Task.Delay(interval, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }
}
