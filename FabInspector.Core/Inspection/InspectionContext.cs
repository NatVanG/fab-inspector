using System.Net.Http;
using FabInspector.Core.Part;

namespace FabInspector.Core.Inspection
{
    /// <summary>
    /// Per-inspection-run state. Constructed once at the start of a run and treated as
    /// immutable for the duration of that run. Replaces the scattered <c>AsyncLocal</c>
    /// slots on the legacy <c>ContextService</c> with a single ambient POCO that
    /// operators read through <see cref="InspectionContextHolder.Current"/>.
    /// </summary>
    /// <remarks>
    /// Operators cannot receive this via constructor injection because Json.Logic
    /// constructs <see cref="Json.Logic.Rule"/> instances through a
    /// <see cref="System.Text.Json.Serialization.JsonConverter"/> outside of the DI
    /// container. The accessor pattern (a single <see cref="System.Threading.AsyncLocal{T}"/>
    /// slot exposed through <see cref="InspectionContextHolder"/>) is the smallest
    /// surface that keeps operators state-free at construction while still giving
    /// hosts a per-run state container.
    /// </remarks>
    public sealed record InspectionContext
    {
        /// <summary>
        /// Process-wide HTTP client. Set by the inspection engine from a DI singleton
        /// so concurrent runs reuse one client instance (avoiding socket exhaustion).
        /// </summary>
        public required HttpClient HttpClient { get; init; }
        /// <summary>
        /// Fabric workspace ID (GUID) targeted by this inspection run. May be empty for
        /// purely local inspections that read files from disk.
        /// </summary>
        public required string FabricWorkspaceId { get; init; }

        /// <summary>
        /// Fabric item file path (local) or item ID (remote). Mutable because
        /// <see cref="Inspector"/> overwrites it per discovered fabric item during
        /// a workspace-scoped run.
        /// </summary>
        public string? FabricItem { get; set; }

        /// <summary>
        /// Token provider used by operators that call authenticated APIs. Required for
        /// any remote operator (apiget, daxquery, dfsget, scannerapi, sqlquery).
        /// </summary>
        public required ITokenProvider TokenProvider { get; init; }

        /// <summary>
        /// Reporter for progress messages emitted by operators (e.g. "Starting GET request").
        /// Optional — when null, operator progress messages are dropped.
        /// </summary>
        public IInspectionMessageReporter? MessageReporter { get; set; }

        /// <summary>
        /// The current rule's name. Set by the inspection engine just before the rule's
        /// JsonLogic expression is evaluated; consumed by progress message formatting.
        /// </summary>
        public string? RuleName { get; set; }

        /// <summary>
        /// File-system path of the part currently being inspected. Set per-part by the
        /// inspection engine; consumed by progress message formatting.
        /// </summary>
        public string? ItemPath { get; set; }

        /// <summary>
        /// The part query backing the current rule traversal, when applicable.
        /// </summary>
        public IPartQuery? PartQuery { get; set; }

        /// <summary>
        /// The part currently under inspection, when applicable. Mutated per part
        /// within a rule's traversal.
        /// </summary>
        public Part.Part? Part { get; set; }
    }
}
