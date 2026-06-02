using FabInspector.Web.Workload.Jobs;

namespace FabInspector.Tests.Workload;

/// <summary>
/// Contract tests for the in-memory <see cref="IJobRunStore"/> implementation.
/// These tests pin the async surface introduced in Phase 3 so that the
/// behaviour callers depend on (live-record sharing, list ordering, save
/// no-op) stays stable as the durable Azure Tables store evolves.
/// </summary>
[TestFixture]
public sealed class InMemoryJobRunStoreTests
{
    private const string ItemType = "FabInspectorRuleSet";
    private const string JobType = "FabInspector.FabInspectorRuleSet.RunRules";

    [Test]
    public async Task CreateAsync_ReturnsRecordAndMakesItRetrievable()
    {
        var store = new InMemoryJobRunStore();
        var workspaceId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var jobInstanceId = Guid.NewGuid();

        var created = await store.CreateAsync(ItemType, workspaceId, itemId, JobType, jobInstanceId);

        Assert.Multiple(() =>
        {
            Assert.That(created.JobInstanceId, Is.EqualTo(jobInstanceId));
            Assert.That(created.Status, Is.EqualTo(JobStatus.NotStarted));
            Assert.That(created.WorkspaceId, Is.EqualTo(workspaceId));
            Assert.That(created.ItemId, Is.EqualTo(itemId));
        });

        var fetched = await store.GetAsync(jobInstanceId);
        Assert.That(fetched, Is.SameAs(created), "GetAsync must return the live in-process record so callers share the same CancellationTokenSource.");
    }

    [Test]
    public async Task GetAsync_ReturnsNullForUnknownId()
    {
        var store = new InMemoryJobRunStore();
        var result = await store.GetAsync(Guid.NewGuid());
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ListAsync_FiltersByWorkspaceAndItemAndOrdersByStartTimeDescending()
    {
        var store = new InMemoryJobRunStore();
        var ws = Guid.NewGuid();
        var item = Guid.NewGuid();

        // Two runs for the target (ws,item), one for a different item.
        var older = await store.CreateAsync(ItemType, ws, item, JobType, Guid.NewGuid());
        older.StartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-10);

        var newer = await store.CreateAsync(ItemType, ws, item, JobType, Guid.NewGuid());
        newer.StartTimeUtc = DateTimeOffset.UtcNow;

        await store.CreateAsync(ItemType, ws, Guid.NewGuid(), JobType, Guid.NewGuid());

        var list = (await store.ListAsync(ws, item)).ToList();

        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0], Is.SameAs(newer), "Newest run should be listed first.");
        Assert.That(list[1], Is.SameAs(older));
    }

    [Test]
    public async Task SaveAsync_IsNoOpForInMemoryStore()
    {
        // In-memory mutations happen on the live record returned by GetAsync,
        // so SaveAsync must succeed without doing anything observable. This
        // pins the contract that other call sites (controller + inspection
        // service) can SaveAsync freely without side-effects in dev.
        var store = new InMemoryJobRunStore();
        var record = await store.CreateAsync(ItemType, Guid.NewGuid(), Guid.NewGuid(), JobType, Guid.NewGuid());

        record.Status = JobStatus.Succeeded;
        record.PassCount = 7;
        record.FailCount = 1;

        await store.SaveAsync(record);

        var fetched = await store.GetAsync(record.JobInstanceId);
        Assert.That(fetched, Is.SameAs(record));
        Assert.That(fetched!.Status, Is.EqualTo(JobStatus.Succeeded));
        Assert.That(fetched.PassCount, Is.EqualTo(7));
    }
}
