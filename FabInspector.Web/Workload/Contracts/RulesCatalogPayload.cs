using System.Text.Json.Serialization;

namespace FabInspector.Web.Workload.Contracts;

/// <summary>
/// JSON shape stored in the <c>catalog.json</c> part of a
/// <c>FabInspectorRulesCatalog</c> item. Each entry is a pointer to a
/// <c>FabInspectorRuleSet</c> item by workspace + item id. Pointers are
/// resolved at run time; they are not treated as a hard foreign key.
/// </summary>
public sealed class RulesCatalogPayload
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("ruleSets")]
    public List<RulesCatalogPointer> RuleSets { get; set; } = new();
}

public sealed class RulesCatalogPointer
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; set; } = string.Empty;

    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
}
