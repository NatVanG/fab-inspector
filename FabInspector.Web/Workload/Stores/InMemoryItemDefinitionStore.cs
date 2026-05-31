using System.Collections.Concurrent;
using FabInspector.Web.Workload.Contracts;

namespace FabInspector.Web.Workload.Stores;

internal sealed class InMemoryItemDefinitionStore : IItemDefinitionStore
{
    private sealed record Key(string ItemType, Guid WorkspaceId, Guid ItemId);

    private sealed class Entry
    {
        public ItemDefinitionEnvelope Envelope { get; set; } = new();
        public bool IsSoftDeleted { get; set; }
    }

    private readonly ConcurrentDictionary<Key, Entry> _items = new();

    public Task UpsertAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope envelope, CancellationToken ct = default)
    {
        _items[new Key(itemType, workspaceId, itemId)] = new Entry { Envelope = envelope, IsSoftDeleted = false };
        return Task.CompletedTask;
    }

    public Task<ItemDefinitionEnvelope?> GetAsync(string itemType, Guid workspaceId, Guid itemId, CancellationToken ct = default)
    {
        if (_items.TryGetValue(new Key(itemType, workspaceId, itemId), out var entry) && !entry.IsSoftDeleted)
        {
            return Task.FromResult<ItemDefinitionEnvelope?>(entry.Envelope);
        }
        return Task.FromResult<ItemDefinitionEnvelope?>(null);
    }

    public Task DeleteAsync(string itemType, Guid workspaceId, Guid itemId, bool hard, CancellationToken ct = default)
    {
        var key = new Key(itemType, workspaceId, itemId);
        if (hard)
        {
            _items.TryRemove(key, out _);
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
            if (envelope != null) entry.Envelope = envelope;
        }
        else if (envelope != null)
        {
            _items[key] = new Entry { Envelope = envelope, IsSoftDeleted = false };
        }
        return Task.CompletedTask;
    }
}
