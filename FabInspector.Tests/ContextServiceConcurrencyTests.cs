using FabInspector.Core.Part;
using NUnit.Framework;

namespace FabInspector.Tests;

/// <summary>
/// Regression guard for the Phase 0 ContextService refactor (ThreadLocal -> AsyncLocal).
/// Ensures concurrent inspection runs cannot leak per-run state across async flows or
/// reused thread-pool threads.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ContextServiceConcurrencyTests
{
    [Test]
    public async Task FabricWorkspaceId_DoesNotLeakAcrossConcurrentRuns()
    {
        const int runs = 32;
        var tasks = Enumerable.Range(0, runs).Select(async i =>
        {
            var workspaceId = $"workspace-{i:D4}";
            var itemId = $"item-{i:D4}";

            using (ContextService.BeginScope(tokenProvider: null, fabricWorkspaceId: workspaceId, fabricItem: itemId))
            {
                // Force multiple thread hops to expose any ThreadLocal-style leakage.
                await Task.Yield();
                Assert.That(ContextService.FabricWorkspaceId, Is.EqualTo(workspaceId));
                Assert.That(ContextService.FabricItem, Is.EqualTo(itemId));

                await Task.Delay(1).ConfigureAwait(false);
                Assert.That(ContextService.FabricWorkspaceId, Is.EqualTo(workspaceId));

                await Task.Yield();
                Assert.That(ContextService.FabricItem, Is.EqualTo(itemId));
            }
        }).ToArray();

        await Task.WhenAll(tasks);
    }

    [Test]
    public async Task BeginScope_RestoresPriorValuesOnDispose()
    {
        ContextService.FabricWorkspaceId = "outer-workspace";
        try
        {
            using (ContextService.BeginScope(tokenProvider: null, fabricWorkspaceId: "inner-workspace", fabricItem: null))
            {
                await Task.Yield();
                Assert.That(ContextService.FabricWorkspaceId, Is.EqualTo("inner-workspace"));
            }

            Assert.That(ContextService.FabricWorkspaceId, Is.EqualTo("outer-workspace"));
        }
        finally
        {
            ContextService.FabricWorkspaceId = null;
        }
    }

    [Test]
    public async Task BeginScope_WithPartContext_SeedsAllAsyncLocals()
    {
        var partContext = new PartContext
        {
            PartQuery = null!,
            Part = null!,
            FabricWorkspaceId = "ctx-workspace",
            FabricItem = "ctx-item"
        };

        using (ContextService.BeginScope(partContext))
        {
            await Task.Yield();
            Assert.That(ContextService.Current, Is.SameAs(partContext));
            Assert.That(ContextService.FabricWorkspaceId, Is.EqualTo("ctx-workspace"));
            Assert.That(ContextService.FabricItem, Is.EqualTo("ctx-item"));
        }

        Assert.That(ContextService.Current, Is.Null);
        Assert.That(ContextService.FabricWorkspaceId, Is.Null);
        Assert.That(ContextService.FabricItem, Is.Null);
    }
}
