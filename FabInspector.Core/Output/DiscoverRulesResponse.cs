namespace FabInspector.Core.Output
{
    public sealed class DiscoverRulesResponse
    {
        public string SchemaVersion { get; set; } = "1";

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public string? FabricItem { get; set; }

        public string? RulesFilePath { get; set; }

        public string? RulesCatalogPath { get; set; }

        public IReadOnlyList<string> TargetItemTypes { get; set; } = [];

        public IReadOnlyList<DiscoverRuleMetadata> Rules { get; set; } = [];
    }

    public sealed class DiscoverRuleMetadata
    {
        public string? RuleId { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public string Severity { get; set; } = "warning";

        public IReadOnlyList<string> ItemTypes { get; set; } = [];

        public IReadOnlyList<string> Tags { get; set; } = [];

        public string? SourcePath { get; set; }

        public string? RuleSetName { get; set; }

        public DiscoverRuleTestMetadata Test { get; set; } = new();

        public DiscoverRulePartScopeHint PartScope { get; set; } = new();

        public required string InclusionReason { get; set; }

        public required string GuidanceSummary { get; set; }
    }

    public sealed class DiscoverRuleTestMetadata
    {
        public string Logic { get; set; } = string.Empty;

        public System.Text.Json.Nodes.JsonNode? Data { get; set; }

        public System.Text.Json.Nodes.JsonNode? Expected { get; set; }
    }

    public sealed class DiscoverRulePartScopeHint
    {
        public string? Part { get; set; }

        public bool PathErrorWhenNoMatch { get; set; }

        public bool AppliesToRootPart { get; set; }
    }
}