| name | fab-inspector-rules |
|------|---------------------|
| description | Author Fab Inspector rules using JSONLogic to test Microsoft Fabric item definitions. Activate when users need to write, edit, debug, or understand inspection rules, operators, or the rule file format. |

# Fab Inspector Rule Authoring

This skill provides comprehensive guidance for writing Fab Inspector rules — deterministic, JSONLogic-based tests that validate Microsoft Fabric item definitions.

## 0 — Fabric MCP grounding

Before creating, editing, or correcting Fab Inspector rules for Fabric items or Fabric REST APIs:

- Ensure the local Fabric MCP server is installed and available in the current environment.
- Consult the local Fabric MCP `docs_item_definitions` tool when identifying valid definition parts, payload shapes, JSON Pointer paths, and example property locations.
- Consult the local Fabric MCP `docs_platform_api_spec` tool when the rule targets core Fabric platform APIs.
- Consult the local Fabric MCP `docs_workload_api_spec` tool when the rule targets workload-specific Fabric APIs.
- Use these local docs sources as needed to keep rule parts, JSON paths, and API endpoints aligned with current official Fabric contracts.

Installation baseline (required):

Use the official instructions from:
https://github.com/microsoft/mcp/blob/main/servers/Fabric.Mcp.Server/README.md#installation

For VS Code (recommended), ensure all of the following are true before authoring rules:

1. The `GitHub Copilot Chat` extension is installed.
2. The `Fabric MCP Server` extension is installed.
3. In Copilot Chat Agent mode, refresh the tools list and confirm Fabric MCP tools are present.
4. Validate with a simple Fabric MCP question (for example: "What Fabric workload types are available?") to confirm the server is active.

If Fabric MCP tools are unavailable, stop rule authoring work and complete installation/troubleshooting first.

Practical usage:

- Use `docs_item_definitions` for `itemType`, `part`, `var`, JSON Pointer, path accuracy.
- Use `docs_platform_api_spec` for `apiget` rules against shared/core Fabric endpoints.
- Use `docs_workload_api_spec` for `apiget` rules against workload-specific endpoints and payloads.

## 1 — Rules file structure

A rules file is a JSON document containing a `rules` array:

```json
{
  "rules": [
    { /* rule 1 */ },
    { /* rule 2 */ }
  ]
}
```

Each rule object has these properties:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `id` | string | Yes | Unique identifier for the rule (e.g., `"CHECK_THEME_NAME"`). |
| `name` | string | Yes | Human-readable name shown in HTML results and wireframe images. |
| `description` | string | No | Detailed explanation of what the rule tests. |
| `logType` | string | No | `"error"` or `"warning"` (default: `"warning"`). |
| `itemType` | string | No | Fabric item type: `Report`, `CopyJob`, `Lakehouse`, `SemanticModel`, `DataPipeline`, `Dataflow`, `SQLDatabase`, `VariableLibrary`, etc. Use `"*"` for cross-item rules or `"json"` for any JSON file. Use `"none"` for API-only rules. |
| `disabled` | boolean | No | `true` to skip this rule (default: `false`). |
| `part` | string | No | Iterator. A reserved Report part name **or** a regular expression to match file/folder paths. See Section 2. |
| `applyPatch` | boolean | No | `true` to enable patch application (default: `false`). |
| `test` | array | Yes | Test definition — a 2- or 3-element array. See Section 3. |
| `patch` | array | No | Optional patch to fix failing items. See Section 7. |

### Minimal rule example

```json
{
  "id": "SIMPLEST_RULE",
  "name": "Simplest Rule",
  "test": [
    true,
    true
  ]
}
```

## 2 — The `part` iterator

The `part` property controls **which files or parts** the rule runs against. If a part matches multiple items, the rule runs iteratively against each one.

### Reserved Report part names (case-sensitive)

When `itemType` is `Report`, these reserved names are available:

| Part name | Returns |
|-----------|---------|
| `Report` | The `report.json` file content |
| `ReportExtensions` | The `reportExtensions.json` file content |
| `Pages` | List of all `*.page.json` files (iterates each page) |
| `PagesHeader` | The `pages.json` collection header |
| `AllPages` | All pages as a single array |
| `Visuals` | Visual files (`*.visual.json`) in the context of the current part iterator |
| `AllVisuals` | All visuals across the entire report |
| `MobileVisuals` | Mobile visual files (`*.mobile.json`) in the current part context |
| `AllMobileVisuals` | All mobile visuals across the report |
| `Bookmarks` | Bookmark files (`*.bookmark.json`) in the current part context |
| `BookmarksHeader` | The `bookmarks.json` collection header |
| `AllBookmarks` | All bookmarks across the report |
| `Files` | List of all files within the parent part context |

### Regex-based file matching

For non-Report item types (or to match specific files within any item), use a **regular expression**. Folder separators are normalised to the colon character (`:`) for cross-platform compatibility.

```json
"part": "copyjob-content.json"
```

Matches any path ending in `copyjob-content.json`.

```json
"part": "folder1:.*:copyjob-content\\.json$"
```

Matches `copyjob-content.json` files under any subfolder of `folder1`.

This colon-based normalisation means:
- Windows path `C:\fabricproject\folder1\copyjob1.CopyJob\copyjob-content.json`
- Linux path `/home/fabricproject/folder1/copyjob1.CopyJob/copyjob-content.json`

Both match `folder1:.*:copyjob-content.json`.

### File extension matching

```json
"part": ".pq$"
```

Matches all PowerQuery files. Useful for Dataflow inspections.

```json
"part": ".sql"
```

Matches all SQL files. Useful for SQLDatabase inspections.

## 3 — Test definition

A test is an array with **2 or 3 elements**:

```json
"test": [
  { /* logic (JSONLogic expression) */ },
  { /* data mapping (optional) */ },
  /* expected result */
]
```

| Element | Required | Description |
|---------|----------|-------------|
| Logic | Yes | A JSONLogic expression that produces a result. |
| Data mapping | No | Named variables bound via JSON Pointer paths. Can be omitted (2-element test). |
| Expected result | Yes | The value the logic must produce for the test to pass. |

The test **passes** when the logic output matches the expected result exactly.

### The `var` operator

The `var` operator accesses values from the current JSON context:

```json
{ "var": "displayName" }
```

Access nested properties with dot notation:

```json
{ "var": "properties.jobMode" }
```

An empty `var` returns the root item:

```json
{ "var": "" }
```

### Data mapping (optional second element)

Bind named variables using JSON Pointer paths (prefixed with `/`):

```json
{
  "customvis": "/publicCustomVisuals"
}
```

Or literal values:

```json
{
  "entities": "/entities"
}
```

These variables are then available via `var` in the logic expression.

### Expected result patterns

| Pattern | Meaning |
|---------|---------|
| `true` | Boolean assertion |
| `"someString"` | Exact string match |
| `4` | Exact numeric match |
| `[]` | Empty array — "no failures found" (most common for filter-based rules) |
| `["a", "b"]` | Exact array match |
| `{ "key": "val" }` | Exact object match |

**Best practice**: Design rules to return an **array of failing items** (e.g., visual names) with an expected result of `[]`. This makes results actionable — you see exactly what failed.

## 4 — Built-in JSONLogic operators

Fab Inspector uses JSONLogic as its expression language. All [standard JSONLogic operators](https://jsonlogic.com/operations.html) are available:

| Category | Operators |
|----------|-----------|
| Comparison | `==`, `===`, `!=`, `!==`, `<`, `<=`, `>`, `>=` |
| Logic | `if`, `and`, `or`, `!`, `!!`, `not` |
| Arithmetic | `+`, `-`, `*`, `/`, `%`, `min`, `max` |
| Array | `map`, `filter`, `reduce`, `all`, `some`, `none`, `merge`, `in` |
| String | `cat`, `substr` |
| Data | `var`, `missing`, `missing_some` |
| Other | `log` |

The JSONLogicExample.json file contains examples of JSONLogic expressions, data mappings (optional) and their expected results.

## 5 — Custom operators reference

Fab Inspector adds these custom operators beyond standard JSONLogic. Grouped by category.

### Navigation operators

#### `part`
Returns file content of a Fabric CI/CD item. Within `test` logic, use it to access specific parts. An part set to an empty string (`""`) refers to the current part from the rule-level iterator.

```json
{ "part": "Visuals" }
{ "part": "Pages" }
{ "part": "Report" }
{ "part": "copyjob-content.json" }
{ "part": "" }           // Current part (from rule-level iterator)
```

#### `partinfo`
Returns metadata about a part instead of its content. Metadata includes:

```json
{
  "filesystemname": "Date.sql",
  "filesystempath": "C:\\...\\dbo\\Tables\\Date.sql",
  "partfilesystemtype": "File",
  "filesize": 1030,
  "filecount": 1
}
```

Usage: `{ "partinfo": "" }` returns metadata for the current part.

#### `query`
Applies an operator to the output of another operator. This is useful when a single Json object is returned instead of an array. Takes two parameters: the output of the first operator and the operator to apply.

```json
{
  "query": [
    { "part": "Version" },
    { "var": "version" }
  ]
}
```

#### `path`
JSONPath expression to query the current context. Uses [JsonPath.Net](https://docs.json-everything.net/path/basics/) / [RFC 9535](https://www.rfc-editor.org/rfc/rfc9535.html) syntax.

```json
{ "path": "$..projections[*]" }
{ "path": "$.*.properties.jobMode" }
```

To apply a JSONPath to a specific data node use in conjunction with the `query` operator:

```json
{ "query": [ {  "var": "response" }, { "path": "$.value[*].displayName" } ] }
```

#### `drillvar`
Parses stringified JSON on the right of a `>` character in a JSON pointer path. Useful for querying escaped JSON (e.g., Deneb custom visual definitions).

### Set operators

#### `diff`
Set difference: returns items in the first array that are not in the second.

```json
{ "diff": [["a","b","c","d"], ["d","c"]] }
// Result: ["a","b"]
```

#### `equalsets`
Tests if two arrays are equal as unordered sets.

```json
{ "equalsets": [["a","b","c"], ["c","a","b"]] }
// Result: true
```

#### `intersection`
Set intersection: returns items present in both arrays.

```json
{ "intersection": [["a","b","c","d"], ["d","c","f","e"]] }
// Result: ["c","d"]
```

#### `symdiff`
Symmetric difference: returns items in either array but not in both.

```json
{ "symdiff": [["a","b","c","d"], ["d","c","f","e"]] }
// Result: ["a","b","f","e"]
```

#### `union`
Set union: returns all unique items from both arrays.

```json
{ "union": [["a","b","c","d"], ["d","c","f","e"]] }
// Result: ["a","b","c","d","f","e"]
```

### String operators

#### `strcontains`
Counts regex matches in a string. Returns the match count.

```json
{ "strcontains": ["The quick brown brown fox", "brown"] }
// Result: 2
```

#### `strsplit`
Splits a string by a delimiter. Returns an array.

```json
{ "strsplit": [{ "var": "name" }, ","] }
```

#### `strjoin`
Joins an array of strings with a separator.

```json
{ "strjoin": [["a", "b", "c"], ","] }
// Result: "a,b,c"
```

#### `regexextract`
Extracts matches from a string using a regex pattern.

#### `tostring`
Converts a JSON node to its stringified JSON representation.

```json
{ "tostring": [["a","b","c","d"]] }
// Result: "[\"a\",\"b\",\"c\",\"d\"]"
```

### Data operators

#### `count`
Counts items in an array.

```json
{ "count": [["a","b","c","d"]] }
// Result: 4
```

#### `torecord`
Constructs a JSON record from key/value pairs.

```json
{
  "torecord": [
    "isPersistentUserStateDisabled", { "var": "/settings/isPersistentUserStateDisabled" },
    "hideVisualContainerHeader", { "var": "/settings/hideVisualContainerHeader" }
  ]
}
```

#### `distinct`
Returns unique values from an array.

#### `keys`
Returns the keys of a JSON object.

#### `values`
Returns the values of a JSON object.

#### `typeof`
Returns the type of a JSON node as a string.

#### `hasprop`
Checks if a JSON object has a specific property.

#### `isnullorempty`
Tests whether a value is null or empty.

#### `coalesce`
Returns the first non-null value from a list of expressions.

#### `slice`
Extracts a slice of an array (start/end indices).

### Variable binding

#### `let`
Binds computed values to named variables for reuse within the same expression. Avoids duplicate computation (e.g., multiple API calls).

**Basic binding:**

```json
{
  "let": [
    { "items": { "path": "$.visuals[*].name" } },
    { "count": [{ "var": "items" }] }
  ]
}
```

**Multiple bindings:**

```json
{
  "let": [
    {
      "themeName": { "var": "themeCollection.baseTheme.name" },
      "version": { "var": "version" }
    },
    {
      "strjoin": [
        [{ "var": "themeName" }, " @ v", { "var": "version" }],
        ""
      ]
    }
  ]
}
```

**Avoid duplicate API calls:**

```json
{
  "let": [
    {
      "response": { "apiget": ["https://api.fabric.microsoft.com/v1/admin/tenantsettings", "{context-token}"] }
    },
    {
      "and": [
        { "!": [{ "isnullorempty": [{ "var": "response" }] }] },
        { "==": [{ "path": ["$.tenantsettings[?(@.settingName=='AllowSPNs')].enabled", { "var": "response" }] }, true] }
      ]
    }
  ]
}
```

### File system operators

#### `filesize`
Returns file size in bytes. Accepts a file path string or the output of `partinfo`.

```json
{ "filesize": [{ "partinfo": "" }] }
```

#### `filetextsearchcount`
Counts regex matches in a file's text content. Takes a file path (or `partinfo` output) and a regex pattern.

```json
{ "filetextsearchcount": [{ "partinfo": "" }, "ErrorLogID"] }
```

#### `fromyamlfile`
Reads and parses a YAML file, returning its content as JSON.

### Date/time operators

#### `now`
Returns the current UTC date/time.

#### `datediff`
Calculates the difference between two dates.

### Layout operators

#### `rectoverlap`
Detects overlapping visuals on a report page. Takes an array of rectangle records (with `name`, `x`, `y`, `width`, `height` properties) and an optional margin width.

```json
{
  "rectoverlap": [
    {
      "map": [
        { "filter": [{ "part": "Visuals" }, { /* visibility checks */ }] },
        {
          "torecord": [
            "name", { "var": "name" },
            "x", { "var": "position.x" },
            "y", { "var": "position.y" },
            "width", { "var": "position.width" },
            "height", { "var": "position.height" }
          ]
        }
      ]
    },
    5
  ]
}
```

### REST API operators (require non-local auth)

#### `daxquery`
Executes a DAX query against a Semantic Model. Returns the query result.

```json
{ "daxquery": "EVALUATE VALUES('Employee Country')" }
```

#### `apiget`
Performs an authenticated HTTP GET against any supported Power BI or Fabric REST API endpoint and returns the parsed JSON response.

Supported API surfaces:
- Any GET endpoint under `https://api.powerbi.com/v1.0/myorg/...`
- Any GET endpoint under `https://api.fabric.microsoft.com/v1/...`

There is no path-level whitelist in the operator. If the URL starts with one of those two base URLs, Fab Inspector will issue the GET request with the correct access token scope for that host.

Supported forms:

```json
{ "apiget": "https://api.powerbi.com/v1.0/myorg/groups/{context-fabricworkspace}/reports" }
```

```json
{
  "apiget": [
    "https://api.fabric.microsoft.com/v1/workspaces/{context-fabricworkspace}/items?type={type}&recursive={recursive}",
    "Lakehouse",
    "true"
  ]
}
```

How URL resolution works:
- `{context-fabricworkspace}` is replaced with the workspace GUID from the current CLI/runtime context.
- `{context-fabricitem}` is replaced with the current item GUID from the CLI/runtime context.
- Any remaining placeholders such as `{type}`, `{recursive}`, `{itemId}`, `{reportId}` are filled sequentially from the array arguments in the order the placeholders appear in the URL.
- Placeholder names are descriptive only; substitution is positional, not name-based.
- Query-string placeholders are supported the same way as path placeholders.

Authentication and runtime behavior:
- Requires non-local authentication and a configured `HttpClient`/token provider.
- Fab Inspector adds the bearer token in the request header; do not use `{context-token}` inside the URL template.
- Only HTTP GET is supported by this operator.
- The response body must be JSON; the operator parses and returns it as a JSON node.
- Continuation tokens and pagination are not handled automatically. Use `apiget` for single-page endpoints, or endpoints where the first page is sufficient for the rule.
- Unsupported hosts throw an error before the request is sent.

Common GET patterns the skill should understand when authoring rules:
- Power BI workspace inventory, such as `/groups/{context-fabricworkspace}/reports`, `/datasets`, `/dashboards`, `/dataflows`
- Power BI admin/read APIs that are available through GET
- Fabric workspace/item inventory, such as `/workspaces/{context-fabricworkspace}/items`
- Fabric item metadata/settings endpoints, such as `/workspaces/{context-fabricworkspace}/onelake/settings`
- Fabric resource collections filtered by query-string parameters, such as `?type=Lakehouse&recursive=true`

```json
{ "apiget": "https://api.powerbi.com/v1.0/myorg/groups/{context-fabricworkspace}/reports" }
```

With explicit parameters:

```json
{
  "apiget": [
    "https://api.fabric.microsoft.com/v1/workspaces/{context-fabricworkspace}/items?type={type}&recursive={recursive}",
    "Lakehouse",
    "true"
  ]
}
```

Additional examples:

```json
{ "apiget": ["https://api.fabric.microsoft.com/v1/workspaces/{context-fabricworkspace}/onelake/settings"] }
```

```json
{ "apiget": "https://api.powerbi.com/v1.0/myorg/groups/{context-fabricworkspace}/reports" }
```

Best practice: if a rule only needs a subset of the API response, combine `apiget` with `let`, `path`, `filter`, `map`, or `var` so the test compares a small deterministic projection instead of the full response payload.

#### `dfsget`
Retrieves a file from OneLake DFS endpoint. Supports template variables and path parameters.

```json
{
  "dfsget": [
    "https://onelake.dfs.fabric.microsoft.com/{context-fabricworkspace}/{context-fabricitem}/Files/{folder}/{fileName}",
    "Config",
    "settings.json"
  ]
}
```

#### `scannerapi`
Invokes the Power BI Scanner API to scan a workspace. Multi-step: initiates scan, polls for completion, retrieves results.

```json
{ "scannerapi": "{context-fabricworkspace}" }
```

With all metadata options:

```json
{ "scannerapi": ["<workspace-guid>", true, true, true, true, true] }
```

Parameters: workspace ID, lineage, datasource details, dataset schema, expressions, users.

## 6 — Item types

| `itemType` value | Description |
|------------------|-------------|
| `Report` | Power BI Report (PBIR format). Benefits from reserved part names. |
| `CopyJob` | Fabric CopyJob |
| `Lakehouse` | Fabric Lakehouse |
| `SemanticModel` | Power BI Semantic Model / Dataset |
| `DataPipeline` | Fabric Data Pipeline |
| `VariableLibrary` | Pipeline Variable Library |
| `Dataflow` | Dataflow Gen2 |
| `SQLDatabase` | Fabric SQL Database |
| `Warehouse` | Fabric Warehouse |
| `Notebook` | Fabric Notebook |
| `Environment` | Fabric Environment |
| `*` | Cross-item rule — applies across multiple item types (e.g., `DataPipeline\|VariableLibrary`) |
| `json` | Matches any JSON metadata file |
| `none` | API-only rules that don't inspect item files |

For cross-item rules, separate multiple types with `|`:

```json
"itemtype": "DataPipeline|VariableLibrary"
```

## 7 — Patches (deprecated)

Rules can optionally define a patch to auto-fix failing items. Currently **only Power BI Report parts** are supported.

### Patch structure

```json
"patch": [
  "<target-part>",
  [ /* RFC 6902 JSON Patch operations */ ]
]
```

Target part is one of: `Report`, `ReportExtensions`, `Pages`, `PagesHeader`, `AllPages`, `Visuals`, `AllVisuals`, `MobileVisuals`, `AllMobileVisuals`, `Bookmarks`, `BookmarksHeader`, `AllBookmarks`.

### Patch operations (RFC 6902)

| Operation | Description |
|-----------|-------------|
| `add` | Add a value at a JSON Pointer path |
| `remove` | Remove a value at a path |
| `replace` | Replace a value at a path |
| `move` | Move a value from one path to another |
| `copy` | Copy a value from one path to another |
| `test` | Assert a value at a path |

### Example: fix missing axis titles

```json
{
  "id": "SHOW_AXES_TITLES",
  "name": "Show visual axes titles",
  "part": "Pages",
  "disabled": false,
  "applyPatch": true,
  "test": [
    {
      "map": [
        {
          "filter": [
            { "part": "Visuals" },
            {
              "and": [
                { "in": [{ "var": "visual.visualType" }, ["lineChart", "barChart", "columnChart"]] },
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

**Important**: Set `"applyPatch": true` on the rule to enable patch execution.

## 8 — Rule creation walkthrough

### Step 1: Start with the simplest possible rule

```json
{
  "rules": [
    {
      "id": "TRUE_IS_TRUE",
      "name": "True is true",
      "test": [true, true]
    }
  ]
}
```

### Step 2: Use a JSONLogic operator

```json
{
  "id": "SIMPLE_COMPARISON",
  "name": "Simple comparison",
  "test": [
    { "==": ["a", "a"] },
    true
  ]
}
```

### Step 3: Nest operators

```json
{
  "id": "NESTED_OPERATIONS",
  "name": "Nested operations",
  "test": [
    { "if": [{ "<": [1, 2] }, "smaller", "equalorgreater"] },
    "smaller"
  ]
}
```

### Step 4: Query a Fabric item

Reference the root item with an empty `var`:

```json
{
  "id": "ROOT_FABRIC_ITEM",
  "name": "Root Fabric Item",
  "test": [
    { "var": "" },
    {}
  ]
}
```

### Step 5: Iterate with the part iterator

This rule runs once per report page, checking each page's display name:

```json
{
  "id": "CHECK_PAGE_NAME",
  "name": "Check page display name",
  "part": "Pages",
  "test": [
    { "var": "displayName" },
    "My page display name"
  ]
}
```

### Step 6: Use the part operator in logic

Combine all pages' display names into a single array:

```json
{
  "id": "RETURN_PAGE_NAMES",
  "name": "Return all page names",
  "test": [
    {
      "map": [
        { "part": "Pages" },
        { "var": "displayName" }
      ]
    },
    ["Page 1", "Page 2", "Page 3"]
  ]
}
```

### Step 7: Filter and report failures

Return names of pages with default names (the common pattern):

```json
{
  "id": "GIVE_PAGES_MEANINGFUL_NAMES",
  "name": "Give visible pages meaningful names",
  "test": [
    {
      "map": [
        {
          "filter": [
            { "part": "Pages" },
            {
              "and": [
                { "strcontains": [{ "var": "displayName" }, "^Page [1-9]+$"] },
                { "!=": [{ "var": "visibility" }, "HiddenInViewMode"] }
              ]
            }
          ]
        },
        { "var": "displayName" }
      ]
    },
    {}
    []
  ]
}
```

### Step 8: Fabric (non-Report) item rules

Use a regex-based part iterator for any Fabric item:

```json
{
  "id": "CHECK_COPYJOB_JOBMODE",
  "name": "Check that CopyJob JobMode is Batch",
  "itemType": "CopyJob",
  "part": "copyjob-content.json",
  "test": [
    { "var": "properties.jobMode" },
    "Batch"
  ]
}
```

Restrict to a specific subfolder:

```json
"part": "folder1:.*:copyjob-content\\.json$"
```

### Step 9: Cross-item rules

Compare data across multiple Fabric item types:

```json
{
  "id": "VARLIBRARY_MISSINGREFERENCES",
  "name": "Check pipeline variable library references",
  "itemtype": "DataPipeline|VariableLibrary",
  "test": [
    {
      "diff": [
        { "query": [{ "part": "pipeline-content.json" }, { "path": "$.*.properties.libraryVariables.*.variableName" }] },
        { "query": [{ "part": "variables.json" }, { "path": "$.*.variables[*].name" }] }
      ]
    },
    []
  ]
}
```

### Step 10: API-driven rules

Execute a DAX query:

```json
{
  "id": "EXECUTE_DAX_QUERY",
  "name": "Execute DAX query",
  "itemType": "SemanticModel",
  "test": [
    { "daxquery": "EVALUATE VALUES('Employee Country')" },
    ["Canada"]
  ]
}
```

Call a Fabric REST API:

```json
{
  "id": "CHECK_ONELAKE_SETTINGS",
  "name": "Check OneLake settings",
  "itemType": "none",
  "test": [
    { "apiget": ["https://api.fabric.microsoft.com/v1/workspaces/{context-fabricworkspace}/onelake/settings"] },
    []
  ]
}
```

## 9 — Common patterns and idioms

| Pattern | Technique |
|---------|-----------|
| Return failing item names | `filter` + `map` with `{ "var": "name" }`, expected `[]` |
| Check a property value | `{ "var": "property.path" }`, expected `"value"` |
| Count items | `{ "count": [...] }`, expected number |
| Compare sets | `diff`, `intersection`, `equalsets`, `symdiff`, `union` |
| Query nested JSON | `query` + `part` + `var` or `path` |
| Regex file matching | `"part": "pattern"` with colon folder separators |
| Avoid redundant API calls | `let` to bind API response, then reference via `var` |
| Build structured output | `torecord` with key/value pairs |
| Check file sizes | `filesize` + `partinfo` |
| Search file contents | `filetextsearchcount` + `partinfo` + regex |

## 10 — Reference links

- **Wiki — Anatomy of a rule**: https://github.com/NatVanG/PBI-InspectorV2/wiki/Anatomy-of-a-PBI-Inspector-V2-rule
- **Wiki — Operators**: https://github.com/NatVanG/PBI-InspectorV2/wiki/Operators
- **Wiki — Rule creation tutorial**: https://github.com/NatVanG/PBI-InspectorV2/wiki/Rule-creation-tutorial
- **JSONLogic specification**: https://jsonlogic.com/operations.html
- **JSONPath.Net**: https://docs.json-everything.net/path/basics/
- **JSON Patch (RFC 6902)**: https://tools.ietf.org/html/rfc6902
- **Example rules**: `DocsExamples/` folder in this repository
- **Base rules**: `Rules/Base-rules.json`
- **Operators documentation**: `DocsExamples/FabInspector-Operators.md` and `DocsExamples/Ric-Operators.md`
- **CLI invocation**: see the `fab-inspector-cli` skill in this repository
