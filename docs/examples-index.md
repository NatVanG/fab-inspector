# Fab Inspector Examples Index

This page catalogs sample rule files in [ExampleRules](../ExampleRules) so you can quickly find a starting point by scenario.

## Core Rule Templates

| File | Purpose |
|---|---|
| [21-Rules-Template.json](../ExampleRules/21-Rules-Template.json) | Minimal template for a new rules file |
| [23-RulesCatalog.json](../ExampleRules/23-RulesCatalog.json) | Rules catalog that references multiple rule files |

## Fabric Item Type Examples

| File | Purpose |
|---|---|
| [00-CopyJob-Rules.json](../ExampleRules/00-CopyJob-Rules.json) | Validate CopyJob item metadata |
| [06-Environment-Rules.json](../ExampleRules/06-Environment-Rules.json) | Validate Environment item metadata |
| [10-Fabric-CrossItemTypes-Rules.json](../ExampleRules/10-Fabric-CrossItemTypes-Rules.json) | Cross-item-type checks |
| [19-PBIR-rules.json](../ExampleRules/19-PBIR-rules.json) | PBIR-focused rules |
| [32-SemanticModel-tmdl-Rules.json](../ExampleRules/32-SemanticModel-tmdl-Rules.json) | Semantic model/TMDL checks |
| [13-Lakehouse-Rules.json](../ExampleRules/13-Lakehouse-Rules.json) | Lakehouse sample checks |

## Operator-Focused Examples (FabInspector)

| File | Purpose |
|---|---|
| [18-PBI-apiget-Rules.json](../ExampleRules/18-PBI-apiget-Rules.json) | `apiget` against Power BI endpoints |
| [08-Fabric-apiget-Rules.json](../ExampleRules/08-Fabric-apiget-Rules.json) | `apiget` against Fabric endpoints |
| [05-dfsget-Rules.json](../ExampleRules/05-dfsget-Rules.json) | `dfsget` OneLake DFS reads |
| [01-daxquery-Rules.json](../ExampleRules/01-daxquery-Rules.json) | Basic `daxquery` |
| [26-sqlquery-Rules.json](../ExampleRules/26-sqlquery-Rules.json) | Basic `sqlquery` |
| [25-scannerapi-Rules.json](../ExampleRules/25-scannerapi-Rules.json) | `scannerapi` metadata scan checks |

## Operator-Focused Examples (Ric)

| File | Purpose |
|---|---|
| [07-equalsets-Rules.json](../ExampleRules/07-equalsets-Rules.json) | `equalsets` operator pattern |
| [11-intersect-Rules.json](../ExampleRules/11-intersect-Rules.json) | `intersect` operator pattern |
| [12-intersection-Rules.json](../ExampleRules/12-intersection-Rules.json) | `intersection` operator pattern |
| [15-let-Rules.json](../ExampleRules/15-let-Rules.json) | `let` variable binding patterns |
| [16-MiscOperators-Rules.json](../ExampleRules/16-MiscOperators-Rules.json) | Multiple Ric operator patterns |
| [20-ReportPageFieldMap.json](../ExampleRules/20-ReportPageFieldMap.json) | Report page field mapping pattern |
| [29-symdiff-Rules.json](../ExampleRules/29-symdiff-Rules.json) | `symdiff` (symmetric difference) operator pattern |
| [33-union-Rules.json](../ExampleRules/33-union-Rules.json) | `union` operator pattern |

## Governance, Naming, and Configuration

| File | Purpose |
|---|---|
| [36-Workspace-naming-rules.json](../ExampleRules/36-Workspace-naming-rules.json) | Workspace naming standards |
| [34-VariableLibrary-Rules.json](../ExampleRules/34-VariableLibrary-Rules.json) | Shared variable library patterns |

## Report and Theme Examples

| File | Purpose |
|---|---|
| [22-Report-Rules.json](../ExampleRules/22-Report-Rules.json) | Report validation rules |
| [37-Report-base-rules.json](../ExampleRules/37-Report-base-rules.json) | Base report rules |
| [28-Report-StaticResources-Rules.json](../ExampleRules/28-Report-StaticResources-Rules.json) | Static resource checks |
| [30-Report-theme-Rules.json](../ExampleRules/30-Report-theme-Rules.json) | Report theme validation |
| [31-Report-Themeable-properties-Rules.json](../ExampleRules/31-Report-Themeable-properties-Rules.json) | Themeable properties patterns |

## Advanced Examples

| File | Purpose |
|---|---|
| [35-VaryRuleByFabricItemName-Rules.json](../ExampleRules/35-VaryRuleByFabricItemName-Rules.json) | Dynamic rules based on Fabric item names |

## CI/CD YAML Examples

| File | Purpose |
|---|---|
| [ContinuousIntegration-Rules-PBIR.yml](CICD/ContinuousIntegration-Rules-PBIR.yml) | CI pipeline sample for PBIR rules |
| [ContinuousIntegration-CustomRules-PBIR.yml](CICD/ContinuousIntegration-CustomRules-PBIR.yml) | CI pipeline sample for custom rules |

## Next Step

After choosing a sample, review [Rules Guide](rules-guide.md) for rule schema details and [Operators Overview](operators-overview.md) for operator-family selection guidance.
