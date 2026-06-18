# Fab Inspector — Custom Rules Guide

> For a quick-reference of all available operators see [Ric Operators](Ric-Operators.md) and [FabInspector Operators](FabInspector-Operators.md). For in-depth operator explanations and advanced examples see the [Fab Inspector wiki](https://github.com/NatVanG/fab-inspector/wiki).

---

## Contents

- [Rule object structure](#rule-object-structure)
- [Test logic](#test-logic)
- [Rule file examples](#rule-file-examples)

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
| [23-RulesCatalog.json](../ExampleRules/23-RulesCatalog.json) | Growing library of example rules |
| [00-CopyJob-Rules.json](../ExampleRules/00-CopyJob-Rules.json) | Rules to check CopyJob metadata |
| [06-Environment-Rules.json](../ExampleRules/06-Environment-Rules.json) | Rules for Fabric Environment CI/CD items |
| [10-Fabric-CrossItemTypes-Rules.json](../ExampleRules/10-Fabric-CrossItemTypes-Rules.json) | Rules across multiple Fabric item types |
| [23-RulesCatalog.json](../ExampleRules/23-RulesCatalog.json) | Rules catalog referencing multiple rulesets |
| [21-Rules-Template.json](../ExampleRules/21-Rules-Template.json) | Minimal rules file template |

For a categorized list of all sample files, see [Examples Index](examples-index.md).

For operator-specific examples see the 'See also` links in [Ric Operators](Ric-Operators.md) and [Fab Inspector Operators](FabInspector-Operators.md).

---

*For creating and debugging rules interactively, see the [Fab Inspector VS Code extension](https://github.com/NatVanG/fab-inspector-vscode-ext). For the full wiki see [Fab Inspector wiki](https://github.com/NatVanG/fab-inspector/wiki).*
