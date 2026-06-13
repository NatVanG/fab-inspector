# Fab Inspector — Custom Rules Guide

> For a quick-reference of all available operators see [Ric Operators](../DocsExamples/Ric-Operators.md) and [FabInspector Operators](../DocsExamples/FabInspector-Operators.md). For in-depth operator explanations and advanced examples see the [Fab Inspector wiki](https://github.com/NatVanG/fab-inspector/wiki).

---

## Contents

- [Rule object structure](#rule-object-structure)
- [Test logic](#test-logic)
- [Rule file examples](#rule-file-examples)
- [Patching](#patching) ⚠️ deprecated

---

## Rule object structure

Custom rules are defined in a JSON file as an array of rule objects:

```json
{
  "rules": [
    ...
  ]
}
```

Each rule object has the following properties:

```json
{
  "id": "A unique identifier of your choice for the rule",
  "name": "A name that is shown in HTML results with wireframe images.",
  "description": "Details to help you and others understand what this rule does",
  "logType": "Optional. error|warning(default)",
  "itemType": "[fabricitemtype]. The Fabric item type that the rule applies to as referred to in the item's CI/CD \".platform\" file, e.g. CopyJob, Lakehouse, Report, etc. or specify \"*\" to define a cross-Fabric items rule or \"json\" to define a rule that applies to any JSON metadata file.",
  "disabled": "true|false(default)",
  "part": "Optional iterator. A Regex expression to match one or more Fabric item file or folder path, using \":\" as folder separator. If the itemType is Report, file part abstractions (Report|ReportExtensions|Pages|PagesHeader|AllPages|Visuals|AllVisuals|MobileVisuals|AllMobileVisuals|Bookmarks|BookmarksHeader|AllBookmarks) can be specified instead of a regular expression. If the itemType is SemanticModel, TMDL part abstractions (Definition|Database|Expressions|Model|Relationships|DataSources|Functions|Tables|Cultures|Roles|Perspectives) can be specified. When the part resolves to an array (e.g. Pages, Tables), the rule runs once per element.",
  "test": [
    "// test logic",
    "// data variables (optional)",
    "// expected result"
  ],
  "patch": [
    "// optional patch logic to fix the issue"
  ]
}
```

### `part` path format

Since v2.4.2, Fabric item folder paths are normalised using `:` as the folder separator, making path expressions platform-agnostic across Windows and Linux. Examples:

```
"part": "folder1:.*:copyjob-content.json"
"part": "folder1:.*:copyjob-content\\.json$"
```

Both match a path such as `C:\fabricproject\folder1\copyjob1.CopyJob\copyjob-content.json` on Windows and `/home/fabricproject/folder1/copyjob1.CopyJob/copyjob-content.json` on Linux.

---

## Test logic

The `test` array uses [JSONLogic](https://jsonlogic.com/) for conditional logic, augmented by Ric and FabInspector operators. The standard form is:

```json
"test": [
  { "// expression that produces the actual value": "" },
  { "// optional: intermediate variable bindings": "" },
  "expected-value"
]
```

If the third element is omitted, the test passes when the expression is truthy.

### Example: check chart axes titles

The following rule checks that chart visuals have both axes titles visible, returning an array of failing visual names:

```json
{
  "id": "SHOW_AXES_TITLES",
  "name": "Show visual axes titles",
  "description": "Check that certain charts have both axes title showing.",
  "logType": "error",
  "itemType": "Report",
  "part": "Pages",
  "disabled": false,
  "test": [
    {
      "map": [
        {
          "filter": [
            { "part": "Visuals" },
            {
              "and": [
                {
                  "in": [
                    { "var": "visual.visualType" },
                    ["lineChart", "barChart", "columnChart", "clusteredBarChart", "stackedBarChart"]
                  ]
                },
                {
                  "or": [
                    { "==": [{ "var": "visual.objects.categoryAxis.0.properties.showAxisTitle.expr.Literal.Value" }, "false"] },
                    { "==": [{ "var": "visual.objects.valueAxis.0.properties.showAxisTitle.expr.Literal.Value" }, "false"] }
                  ]
                }
              ]
            }
          ]
        },
        { "var": "name" }
      ]
    },
    {},
    []
  ]
}
```

### Example: vary by report name

Although somewhat of an anti-pattern, rules can vary their test logic based on the item display name retrieved from the `.platform` file:

```json
{
  "id": "VARY_BY_REPORT_NAME",
  "name": "Vary by report name",
  "description": "Run rule only if report display name is 'Inventory sample'",
  "test": [
    {
      "?:": [
        {
          "==": [
            { "query": [{ "part": ".platform" }, { "var": "0.metadata.displayName" }] },
            "Inventory sample"
          ]
        },
        "Rule output",
        "This is another report."
      ]
    },
    "Rule output"
  ]
}
```

---

## Rule file examples

| File | Description |
|---|---|
| [Base Rules](../Rules/Base-rules.json) | The set of rules that ships with Fab Inspector (Power BI report quality rules) |
| [Examples-rules.json](../DocsExamples/Examples-rules.json) | Growing library of example rules |
| [Example-patches.json](../DocsExamples/Example-patches.json) | Examples of patches to fix issues |
| [Example-CopyJob-Rules.json](../DocsExamples/Example-CopyJob-Rules.json) | Rules to check CopyJob metadata |
| [Example-Environment-Rules.json](../DocsExamples/Example-Environment-Rules.json) | Rules for Fabric Environment CI/CD items |
| [Example-FabricCrossItem-Rules.json](../DocsExamples/Example-FabricCrossItem-Rules.json) | Rules across multiple Fabric item types |
| [Example-RulesCatalog.json](../DocsExamples/Example-RulesCatalog.json) | Rules catalog referencing multiple rulesets |
| [RulesTemplate.json](../DocsExamples/RulesTemplate.json) | Minimal rules file template |

For a categorized list of all sample files, see [Examples Index](examples-index.md).

For operator-specific examples see the `See also` links in [Ric Operators](../DocsExamples/Ric-Operators.md) and [FabInspector Operators](../DocsExamples/FabInspector-Operators.md).

---

## Patching

> ⚠️ **Deprecated.** The `patch` mechanism is available but no longer actively developed. Prefer fixing issues at the source rather than using automated patches.

A rule can optionally define a `patch` to automatically fix items failing the test. The patch targets a file part and applies a [JSON Patch](https://tools.ietf.org/html/rfc6902) operation array.

Structure:

```json
"patch": [
  "One of Report|Pages|PagesHeader|AllPages|Visuals|AllVisuals|Bookmarks|BookmarksHeader|AllBookmarks",
  [
    { "op": "replace", "path": "/some/json/pointer", "value": "new-value" }
  ]
]
```

To apply patches, set `"applyPatch": true` on the rule. Use `-parallel false` when patches are active to avoid last-writer-wins conflicts.

### Example: fix axis titles

```json
{
  "id": "SHOW_AXES_TITLES",
  "name": "Show visual axes titles",
  "description": "Check that certain charts have both axes title showing.",
  "part": "Pages",
  "disabled": true,
  "applyPatch": true,
  "test": [
    {
      "map": [
        {
          "filter": [
            { "part": "Visuals" },
            {
              "and": [
                {
                  "in": [
                    { "var": "visual.visualType" },
                    ["lineChart", "barChart", "columnChart", "clusteredBarChart", "stackedBarChart"]
                  ]
                },
                {
                  "or": [
                    { "==": [{ "var": "visual.objects.categoryAxis.0.properties.showAxisTitle.expr.Literal.Value" }, "false"] },
                    { "==": [{ "var": "visual.objects.valueAxis.0.properties.showAxisTitle.expr.Literal.Value" }, "false"] }
                  ]
                }
              ]
            }
          ]
        },
        { "var": "name" }
      ]
    },
    {},
    []
  ],
  "patch": [
    "Visuals",
    [
      { "op": "replace", "path": "/visual/objects/categoryAxis/0/properties/showAxisTitle/expr/Literal/Value", "value": "true" },
      { "op": "replace", "path": "/visual/objects/valueAxis/0/properties/showAxisTitle/expr/Literal/Value", "value": "true" }
    ]
  ]
}
```

### Example: set active report page

```json
{
  "id": "ACTIVE_PAGE",
  "name": "Ensure report's active page index is set to the correct page",
  "description": "",
  "part": "PagesHeader",
  "applyPatch": true,
  "test": [
    { "var": "activePageName" },
    "ReportSection89a9619c7025093ade1c"
  ],
  "patch": [
    "PagesHeader",
    [
      { "op": "replace", "path": "/activePageName", "value": "ReportSection89a9619c7025093ade1c" }
    ]
  ]
}
```

---

*For creating and debugging rules interactively, see the [Fab Inspector VS Code extension](https://github.com/NatVanG/fab-inspector-vscode-ext). For the full wiki see [Fab Inspector wiki](https://github.com/NatVanG/fab-inspector/wiki).*
