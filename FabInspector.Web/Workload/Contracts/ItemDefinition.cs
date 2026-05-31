using System.Text.Json.Serialization;

namespace FabInspector.Web.Workload.Contracts;

/// <summary>
/// Wire-format for a Fabric item definition as supplied by the Extensibility
/// Toolkit lifecycle endpoints. Mirrors the shape documented at
/// https://learn.microsoft.com/fabric/extensibility-toolkit/how-to-enable-remote-item-lifecycle
/// </summary>
public sealed class ItemDefinitionEnvelope
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("definition")]
    public ItemDefinition? Definition { get; set; }
}

public sealed class ItemDefinition
{
    [JsonPropertyName("parts")]
    public List<ItemDefinitionPart> Parts { get; set; } = new();
}

public sealed class ItemDefinitionPart
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>Base64-encoded payload bytes.</summary>
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("payloadType")]
    public string PayloadType { get; set; } = "InlineBase64";
}

public sealed class DeleteItemRequest
{
    [JsonPropertyName("deleteType")]
    public string DeleteType { get; set; } = "Hard"; // Hard | Soft
}
