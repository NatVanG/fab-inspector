# Fab Inspector Examples Index

This page catalogs sample rule files in [DocsExamples](../DocsExamples) so you can quickly find a starting point by scenario.

## Core Rule Templates

| File | Purpose |
|---|---|
| [RulesTemplate.json](../DocsExamples/RulesTemplate.json) | Minimal template for a new rules file |
| [Examples-rules.json](../DocsExamples/Examples-rules.json) | General mixed examples |
| [Example-RulesCatalog.json](../DocsExamples/Example-RulesCatalog.json) | Rules catalog that references multiple rule files |

## Fabric Item Type Examples

| File | Purpose |
|---|---|
| [Example-CopyJob-Rules.json](../DocsExamples/Example-CopyJob-Rules.json) | Validate CopyJob item metadata |
| [Example-Environment-Rules.json](../DocsExamples/Example-Environment-Rules.json) | Validate Environment item metadata |
| [Example-FabricCrossItem-Rules.json](../DocsExamples/Example-FabricCrossItem-Rules.json) | Cross-item-type checks |
| [Example-pbir-rules.json](../DocsExamples/Example-pbir-rules.json) | PBIR-focused rules |
| [Example-tmdl-rules.json](../DocsExamples/Example-tmdl-rules.json) | Semantic model/TMDL checks |
| [Sample-Lakehouse-Rules.json](../DocsExamples/Sample-Lakehouse-Rules.json) | Lakehouse sample checks |

## Operator-Focused Examples (FabInspector)

| File | Purpose |
|---|---|
| [Example-pbi-apiget-rule.json](../DocsExamples/Example-pbi-apiget-rule.json) | `apiget` against Power BI endpoints |
| [Example-fabric-apiget-rule.json](../DocsExamples/Example-fabric-apiget-rule.json) | `apiget` against Fabric endpoints |
| [Example-fabric-apiget-wparams-rule.json](../DocsExamples/Example-fabric-apiget-wparams-rule.json) | `apiget` with URL parameters |
| [Example-dfsget-rule.json](../DocsExamples/Example-dfsget-rule.json) | `dfsget` OneLake DFS reads |
| [Example-daxquery-rule.json](../DocsExamples/Example-daxquery-rule.json) | Basic `daxquery` |
| [Example-daxquery-rule2.json](../DocsExamples/Example-daxquery-rule2.json) | `daxquery` variant |
| [Example-daxquery-rule3.json](../DocsExamples/Example-daxquery-rule3.json) | `daxquery` variant |
| [Example-daxquery-rule4.json](../DocsExamples/Example-daxquery-rule4.json) | `daxquery` variant |
| [Example-sqlquery-rule.json](../DocsExamples/Example-sqlquery-rule.json) | Basic `sqlquery` |
| [Example-sqlquery-wparams-rule.json](../DocsExamples/Example-sqlquery-wparams-rule.json) | `sqlquery` with explicit parameters |
| [Example-scannerapi-rules.json](../DocsExamples/Example-scannerapi-rules.json) | `scannerapi` metadata scan checks |
| [Example-scannerapi-modified-last-week-rule.json](../DocsExamples/Example-scannerapi-modified-last-week-rule.json) | `scannerapi` with modified-date scenario |

## Operator-Focused Examples (Ric)

| File | Purpose |
|---|---|
| [Example-NewOperators-rules.json](../DocsExamples/Example-NewOperators-rules.json) | Multiple Ric operator patterns |
| [Example-let-rule.json](../DocsExamples/Example-let-rule.json) | `let` variable binding patterns |
| [Example-Let-Nested-Sample.json](../DocsExamples/Example-Let-Nested-Sample.json) | Nested `let` patterns |
| [Example-ReportPageFieldMap.json](../DocsExamples/Example-ReportPageFieldMap.json) | Report page field mapping pattern |

## Governance, Naming, and Configuration

| File | Purpose |
|---|---|
| [Example-workspace-naming-rules.json](../DocsExamples/Example-workspace-naming-rules.json) | Workspace naming standards |
| [Example-lakehouse-workspace-naming-rules.json](../DocsExamples/Example-lakehouse-workspace-naming-rules.json) | Lakehouse/workspace naming standards |
| [Sample-VariableLibrary-rules.json](../DocsExamples/Sample-VariableLibrary-rules.json) | Shared variable library patterns |

## Advanced and Utility

| File | Purpose |
|---|---|
| [Example-OneLake-Rule.json](../DocsExamples/Example-OneLake-Rule.json) | OneLake-oriented checks |
| [Example-StaticResources-rules.json](../DocsExamples/Example-StaticResources-rules.json) | Static resource checks |

## CI/CD YAML Examples

| File | Purpose |
|---|---|
| [ContinuousIntegration-Rules-PBIR.yml](../DocsExamples/ContinuousIntegration-Rules-PBIR.yml) | CI pipeline sample for PBIR rules |
| [ContinuousIntegration-CustomRules-PBIR.yml](../DocsExamples/ContinuousIntegration-CustomRules-PBIR.yml) | CI pipeline sample for custom rules |

## Next Step

After choosing a sample, review [Rules Guide](rules-guide.md) for rule schema details and [Operators Overview](operators-overview.md) for operator-family selection guidance.
