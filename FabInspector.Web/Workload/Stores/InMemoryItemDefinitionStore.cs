using System.Collections.Concurrent;
using FabInspector.Web.Workload.Contracts;

namespace FabInspector.Web.Workload.Stores;

internal sealed class InMemoryItemDefinitionStore : IItemDefinitionStore
{
    private sealed record Key(string ItemType, Guid WorkspaceId, Guid ItemId);

    private sealed class Entry
    {
        public ItemDefinitionEnvelope Envelope { get; set; } = new();
        public string ETag { get; set; } = NewETag();
        public bool IsSoftDeleted { get; set; }
    }

    private readonly ConcurrentDictionary<Key, Entry> _items = new();

    // Per-key serialisation: dedupes overlapping Create/Update calls for the
    // same item so an ETag check + write is atomic.
    private readonly ConcurrentDictionary<Key, SemaphoreSlim> _locks = new();

    private static string NewETag() => Guid.NewGuid().ToString("N");

    private SemaphoreSlim LockFor(Key key) =>
        _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

    public async Task UpsertAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope envelope, string? ifMatch = null, CancellationToken ct = default)
    {
        var key = new Key(itemType, workspaceId, itemId);
        var gate = LockFor(key);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrEmpty(ifMatch))
            {
                _items.TryGetValue(key, out var existing);
                var currentETag = existing?.ETag;
                if (!string.Equals(currentETag, ifMatch, StringComparison.Ordinal))
                {
                    throw new ETagMismatchException(
                        $"ETag mismatch for {itemType}/{workspaceId}/{itemId}: supplied '{ifMatch}', current '{currentETag ?? "<none>"}'.");
                }
            }

            _items[key] = new Entry
            {
                Envelope = envelope,
                ETag = NewETag(),
                IsSoftDeleted = false
            };
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<StoredItemDefinition?> GetAsync(string itemType, Guid workspaceId, Guid itemId, CancellationToken ct = default)
    {
        if (_items.TryGetValue(new Key(itemType, workspaceId, itemId), out var entry) && !entry.IsSoftDeleted)
        {
            return Task.FromResult<StoredItemDefinition?>(new StoredItemDefinition(entry.Envelope, entry.ETag));
        }
        return Task.FromResult<StoredItemDefinition?>(null);
    }

    public Task DeleteAsync(string itemType, Guid workspaceId, Guid itemId, bool hard, CancellationToken ct = default)
    {
        var key = new Key(itemType, workspaceId, itemId);
        if (hard)
        {
            _items.TryRemove(key, out _);
            if (_locks.TryRemove(key, out var gate)) gate.Dispose();
        }
        else if (_items.TryGetValue(key, out var entry))
        {
            entry.IsSoftDeleted = true;
        }
        return Task.CompletedTask;
    }

    public Task RestoreAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope? envelope, CancellationToken ct = default)
    {
        var key = new Key(itemType, workspaceId, itemId);
        if (_items.TryGetValue(key, out var entry))
        {
            entry.IsSoftDeleted = false;
            if (envelope != null)
            {
                entry.Envelope = envelope;
                entry.ETag = NewETag();
            }
        }
        else if (envelope != null)
        {
            _items[key] = new Entry { Envelope = envelope, ETag = NewETag(), IsSoftDeleted = false };
        }
        return Task.CompletedTask;
    }
}
