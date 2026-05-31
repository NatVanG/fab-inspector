/**
 * Canonical workload item-type / part / job string constants.
 * Mirrors FabInspector.Web/Workload/WorkloadItemTypes.cs — keep in sync.
 */
export const WorkloadItemTypes = {
    RuleSet: "FabInspectorRuleSet",
    RulesCatalog: "FabInspectorRulesCatalog"
} as const;

export const WorkloadParts = {
    RulesJson: "rules.json",
    CatalogJson: "catalog.json"
} as const;

export const WorkloadJobs = {
    RunRules: "FabInspector.RunRules",
    RunCatalog: "FabInspector.RunCatalog"
} as const;

export type WorkloadItemType = (typeof WorkloadItemTypes)[keyof typeof WorkloadItemTypes];
