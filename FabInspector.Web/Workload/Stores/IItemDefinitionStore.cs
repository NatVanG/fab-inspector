using FabInspector.Web.Workload.Contracts;

namespace FabInspector.Web.Workload.Stores;

/// <summary>
/// Result of an <see cref="IItemDefinitionStore.GetAsync"/> call. Carries the
/// stored envelope and (when the underlying store supports it) the opaque
/// <c>ETag</c> the caller should echo back in a subsequent
/// <see cref="IItemDefinitionStore.UpsertAsync"/> as the <c>ifMatch</c>
/// guard for optimistic concurrency.
/// </summary>
public readonly record struct StoredItemDefinition(ItemDefinitionEnvelope Envelope, string? ETag);

/// <summary>
/// Thrown by <see cref="IItemDefinitionStore.UpsertAsync"/> when the caller
/// supplied an <c>ifMatch</c> ETag that does not match the currently stored
/// definition. Controllers translate this to <c>412 Precondition Failed</c>.
/// </summary>
public sealed class ETagMismatchException : Exception
{
    public ETagMismatchException(string message) : base(message) { }
}

/// <summary>
/// In-process backing store for the workload's view of a custom item's
/// definition. The authoritative source of truth in production is Fabric
/// itself (the lifecycle endpoints receive every change); this store keeps
/// a local cached copy so the workload's editor and job runner can read
/// the latest definition without round-tripping to Fabric REST on every
/// request.
/// </summary>
public interface IItemDefinitionStore
{
    /// <summary>
    /// Insert or update the stored definition. If <paramref name="ifMatch"/>
    /// is supplied and does not match the currently stored ETag, throws
    /// <see cref="ETagMismatchException"/>.
    /// </summary>
    Task UpsertAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope envelope, string? ifMatch = null, CancellationToken ct = default);

    Task<StoredItemDefinition?> GetAsync(string itemType, Guid workspaceId, Guid itemId, CancellationToken ct = default);

    Task DeleteAsync(string itemType, Guid workspaceId, Guid itemId, bool hard, CancellationToken ct = default);

    Task RestoreAsync(string itemType, Guid workspaceId, Guid itemId, ItemDefinitionEnvelope? envelope, CancellationToken ct = default);
}
