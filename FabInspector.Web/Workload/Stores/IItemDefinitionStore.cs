using FabInspector.Web.Workload.Contracts;

namespace FabInspector.Web.Workload.Stores;

/// <summary>
/// In-process backing store for the workload's view of a custom item's
/// definition. The authoritative source of truth in production is Fabric
/// itself (the lifecycle endpoints receive every change); this store keeps
/// a local cached copy so the workload's editor and job runner can read
/// the latest definition without round-tripping to Fabric REST on every
/// request. Process-lifetime only — documented limitation.
/// </summary>
public interface IItemDefinitionStore
{
    Task UpsertAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope envelope, CancellationToken ct = default);

    Task<ItemDefinitionEnvelope?> GetAsync(string itemType, Guid workspaceId, Guid itemId, CancellationToken ct = default);

    Task DeleteAsync(string itemType, Guid workspaceId, Guid itemId, bool hard, CancellationToken ct = default);

    Task RestoreAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope? envelope, CancellationToken ct = default);
}
