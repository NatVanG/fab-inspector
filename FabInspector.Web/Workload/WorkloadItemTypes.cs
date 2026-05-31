namespace FabInspector.Web.Workload;

/// <summary>
/// Canonical names for the custom Fabric item types and job types that this
/// workload defines. Kept in one place so controllers, stores and editor
/// pages all use identical string literals.
/// </summary>
public static class WorkloadItemTypes
{
    public const string RuleSet = "FabInspectorRuleSet";
    public const string RulesCatalog = "FabInspectorRulesCatalog";

    public static bool IsKnown(string itemType) =>
        itemType is RuleSet or RulesCatalog;

    public static class Parts
    {
        public const string RulesJson = "rules.json";
        public const string CatalogJson = "catalog.json";
    }

    public static class Jobs
    {
        public const string RunRules = "FabInspector.RunRules";
        public const string RunCatalog = "FabInspector.RunCatalog";
    }
}
