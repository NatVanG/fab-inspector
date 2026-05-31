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
        // Convention from the Fabric Extensibility Toolkit:
        //   {WorkloadName}.{ItemType}.{JobOperation}
        // Keep these values in sync with:
        //   - Workload/app/items/constants.ts (WorkloadJobs)
        //   - Workload/Manifest/items/*/*.xml  (<JobScheduler><ItemJobType .../></JobScheduler>)
        //   - Workload/Manifest/items/*/*.json (itemSettings.schedule.itemJobType, jobs[].name)
        public const string RunRules = "FabInspector.FabInspectorRuleSet.RunRules";
        public const string RunCatalog = "FabInspector.FabInspectorRulesCatalog.RunCatalog";
    }
}
