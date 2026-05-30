using System.Text.Json.Serialization;

namespace FabInspector.ClientLibrary.Utils
{
    public sealed class RulesCatalog
    {
        public string Name { get; set; } = "Rules Catalog";

        [JsonPropertyName("ruleSets")]
        public List<RuleSetReference> RuleSets { get; set; } = [];
    }
}
