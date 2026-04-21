# FabInspector Operators — Quick Reference

> For in-depth explanations and advanced examples, see the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki).

FabInspector operators extend the [JSON Logic](https://json-everything.net/json-logic) engine with remote API access and layout analysis. They are available in the `test` field of any rule.

**Authentication:** All REST API operators require a non-`local` authentication method (e.g. `interactive`, `clientsecret`, `certificate`, `federatedtoken`, or `managedidentity`). See the [CLI documentation](../README.md#cli) for authentication options.

**URL placeholder tokens** are automatically resolved at runtime:

| Token | Resolved to |
|---|---|
| `{context-fabricworkspace}` | The workspace ID from the `-fabricworkspace` CLI parameter |
| `{context-fabricitem}` | The item ID from the `-fabricitem` CLI parameter |

---

## Contents

- [REST API Operators](#rest-api-operators): `apiget`, `dfsget`, `daxquery`, `scannerapi`
- [Layout & Geometry](#layout--geometry): `rectoverlap`

---

## REST API Operators

### `apiget`

Performs an authenticated HTTP GET against the Power BI or Fabric REST API and returns the parsed JSON response.

**Two forms:**

| Form | When to use |
|---|---|
| Simple string | URL requires no runtime parameters |
| Array | URL contains additional placeholders such as `{type}`, `{recursive}`, `{folder}`, `{fileName}` to fill from a parameter list |

| Parameter | Type | Description |
|---|---|---|
| urlTemplate | string | Fully-qualified Power BI or Fabric REST API URL. May contain `{context-fabricworkspace}`, `{context-fabricitem}`, and additional placeholders such as `{type}` or `{recursive}`. |
| urlParameters | string[] | Values substituted into the remaining placeholders in the order they appear in the URL (optional) |

**Returns:** Parsed JSON API response.

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

```json
{ "apiget": ["https://api.fabric.microsoft.com/v1/workspaces/{context-fabricworkspace}/onelake/settings"] }
```

See also: [Example-pbi-apiget-rule.json](Example-pbi-apiget-rule.json), [Example-fabric-apiget-rule.json](Example-fabric-apiget-rule.json), [Example-fabric-apiget-wparams-rule.json](Example-fabric-apiget-wparams-rule.json)

---

### `dfsget`

Performs an authenticated HTTP GET against the OneLake DFS endpoint and returns the parsed JSON response, or a raw string if the response body is not valid JSON.

The URL must use HTTPS and target a host ending in `.dfs.fabric.microsoft.com`.

| Parameter | Type | Description |
|---|---|---|
| urlTemplate | string | OneLake DFS HTTPS URL. May contain `{context-fabricworkspace}`, `{context-fabricitem}`, and additional placeholders such as `{folder}` and `{fileName}`. |
| urlParameters | string[] | Values substituted into the remaining placeholders in the order they appear in the URL (optional) |

**Returns:** Parsed JSON node, or raw string if the response is not JSON.

```json
{
  "dfsget": [
    "https://onelake.dfs.fabric.microsoft.com/{context-fabricworkspace}/{context-fabricitem}/Files/{folder}/{fileName}",
    "Config",
    "settings.json"
  ]
}
```

See also: [Example-dfsget-rule.json](Example-dfsget-rule.json)

---

### `daxquery`

Executes a DAX query against a published Power BI semantic model via the [Power BI ExecuteQueries API](https://learn.microsoft.com/en-us/rest/api/power-bi/datasets/execute-queries) and returns the result set.

**Two forms:**

| Form | When to use |
|---|---|
| Simple string | Uses `{context-fabricworkspace}` and `{context-fabricitem}` for workspace and semantic model |
| Array | Explicit workspace and semantic model GUIDs, with optional settings |

| Parameter | Type | Description |
|---|---|---|
| query | string | DAX query expression |
| workspaceId | string | Workspace GUID (or omit to use `{context-fabricworkspace}`) |
| semanticModelId | string | Semantic model (dataset) GUID (or omit to use `{context-fabricitem}`) |
| includeNulls | boolean | Include null values in the result set (optional, default `false`) |
| impersonatedUserName | string | UPN of the user to impersonate for RLS evaluation (optional) |

**Returns:** Parsed DAX query result (Power BI `ExecuteQueries` response JSON).

```json
{ "daxquery": "EVALUATE VALUES('Product'[Category])" }
```

```json
{
  "daxquery": [
    "EVALUATE VALUES('Product'[Category])",
    "f45498e6-9f62-4bbb-bdb6-6d8a7e3a2703",
    "6a496a15-d00c-4cd6-a731-a3fd79e8fb10",
    true,
    "analyst@contoso.com"
  ]
}
```

See also: [Example-daxquery-rule.json](Example-daxquery-rule.json), [Example-daxquery-rule2.json](Example-daxquery-rule2.json), [Example-daxquery-rule3.json](Example-daxquery-rule3.json), [Example-daxquery-rule4.json](Example-daxquery-rule4.json)

---

### `scannerapi`

Calls the [Power BI Admin Workspace Info API](https://learn.microsoft.com/en-us/rest/api/power-bi/admin/workspace-info-post-workspace-info) to retrieve workspace metadata. The operation is asynchronous: Fab Inspector POSTs the scan request, polls for completion (up to 60 attempts at 5-second intervals), and returns the final result.

> **Note:** Polling can take up to 5 minutes. Using `scannerapi` with `-parallel true` is not recommended, as long-running polls will block worker threads.

| Parameter | Type | Description |
|---|---|---|
| workspaceIds | string \| string[] | Workspace GUID(s) to scan. Pass `""` to use `{context-fabricworkspace}`. Accepts a single GUID string, an array of GUID strings, or a comma-separated GUID list. |
| lineage | boolean | Request lineage information (optional) |
| datasourceDetails | boolean | Request datasource details (optional) |
| datasetSchema | boolean | Request dataset schema (optional) |
| datasetExpressions | boolean | Request dataset expressions, e.g. M queries (optional) |
| getArtifactUsers | boolean | Request artifact user permissions (optional) |

**Returns:** Parsed workspace scan result JSON.

```json
{ "scannerapi": "" }
```

```json
{ "scannerapi": ["ws-guid-1", "ws-guid-2"] }
```

```json
{ "scannerapi": ["", true, true, true, true, true] }
```

See also: [Example-scannerapi-rules.json](Example-scannerapi-rules.json)

---

## Layout & Geometry

### `rectoverlap`

Detects overlapping rectangles in a list of named rectangular regions. Optionally expands each rectangle by a pixel margin before checking for overlaps. Returns the names of any rectangles that overlap with at least one other.

| Parameter | Type | Description |
|---|---|---|
| input | array | Array of rectangle objects, each with integer properties `name`, `x`, `y`, `width`, `height` |
| margin | number | Pixel amount to expand each rectangle on all sides before the overlap check (optional, default `0`) |

**Returns:** Array of `name` values for rectangles that overlap with at least one other rectangle.

```json
{
  "rectoverlap": [
    {
      "map": [
        { "part": "Visuals" },
        {
          "torecord": [
            "name",   { "var": "name" },
            "x",      { "var": "visual.position.x" },
            "y",      { "var": "visual.position.y" },
            "width",  { "var": "visual.position.width" },
            "height", { "var": "visual.position.height" }
          ]
        }
      ]
    },
    5
  ]
}
```

---

*For authentication configuration, CLI parameters, and advanced usage see the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki) and the [CLI documentation](../README.md#cli).*
