using System.Text;
using System.Text.Json;
using FabInspector.Core;
using FabInspector.Core.Output;
using FabInspector.Web.Workload;
using FabInspector.Web.Workload.Contracts;
using FabInspector.Web.Workload.Jobs;
using FabInspector.Web.Workload.Stores;

namespace FabInspector.Web.Workload.Runtime;

/// <summary>
/// Decodes a stored item definition and resolves it to one or more
/// <see cref="InspectionRules"/> instances ready to feed to
/// <see cref="Services.InspectionRunner"/>.
/// </summary>
public sealed class ItemDefinitionResolver
{
    private readonly IItemDefinitionStore _store;
    private readonly ILogger<ItemDefinitionResolver> _logger;

    public ItemDefinitionResolver(IItemDefinitionStore store, ILogger<ItemDefinitionResolver> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Read the <c>rules.json</c> part of a stored <c>FabInspectorRuleSet</c>
    /// item and parse it into an <see cref="InspectionRules"/> instance.
    /// </summary>
    public async Task<InspectionRules?> ResolveRuleSetAsync(Guid workspaceId, Guid itemId, CancellationToken ct = default)
    {
        var stored = await _store.GetAsync(WorkloadItemTypes.RuleSet, workspaceId, itemId, ct).ConfigureAwait(false);
        if (stored?.Envelope?.Definition == null) return null;
        return DecodeRules(stored.Value.Envelope.Definition);
    }

    /// <summary>
    /// Read a catalog item's <c>catalog.json</c> part, resolve each pointer
    /// to its referenced rule-set item, and union the rules into a single
    /// merged <see cref="InspectionRules"/>. Missing or disabled pointers
    /// are skipped with a warning entry in <paramref name="warnings"/>.
    /// </summary>
    public async Task<InspectionRules?> ResolveCatalogAsync(Guid workspaceId, Guid itemId, List<string> warnings, CancellationToken ct = default)
    {
        var stored = await _store.GetAsync(WorkloadItemTypes.RulesCatalog, workspaceId, itemId, ct).ConfigureAwait(false);
        if (stored?.Envelope?.Definition == null) return null;
        var definition = stored.Value.Envelope.Definition;

        var part = definition.Parts
            .FirstOrDefault(p => string.Equals(p.Path, WorkloadItemTypes.Parts.CatalogJson, StringComparison.OrdinalIgnoreCase));
        if (part == null)
        {
            warnings.Add($"Catalog item {itemId} has no '{WorkloadItemTypes.Parts.CatalogJson}' part.");
            return null;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(part.Payload));
        var catalog = JsonSerializer.Deserialize<RulesCatalogPayload>(json);
        if (catalog?.RuleSets == null || catalog.RuleSets.Count == 0)
        {
            warnings.Add($"Catalog item {itemId} is empty.");
            return null;
        }

        var merged = new InspectionRules { Rules = new() };
        foreach (var pointer in catalog.RuleSets)
        {
            if (pointer.Disabled) continue;
            if (!Guid.TryParse(pointer.WorkspaceId, out var ws) || !Guid.TryParse(pointer.ItemId, out var iid))
            {
                warnings.Add($"Skipping pointer '{pointer.DisplayName}' — invalid GUID.");
                continue;
            }
            var rules = await ResolveRuleSetAsync(ws, iid, ct).ConfigureAwait(false);
            if (rules == null)
            {
                warnings.Add($"Skipping pointer '{pointer.DisplayName ?? pointer.ItemId}' — referenced rule set not found in workload cache.");
                continue;
            }
            if (rules.Rules != null) merged.Rules.AddRange(rules.Rules);
        }

        return merged.Rules.Count == 0 ? null : merged;
    }

    private static InspectionRules? DecodeRules(ItemDefinition definition)
    {
        var part = definition.Parts
            .FirstOrDefault(p => string.Equals(p.Path, WorkloadItemTypes.Parts.RulesJson, StringComparison.OrdinalIgnoreCase));
        if (part == null) return null;

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(part.Payload));
        return JsonSerializer.Deserialize<InspectionRules>(json);
    }
}
