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

- [REST API Operators](#rest-api-operators): `apiget`, `dfsget`, `daxquery`, `sqlquery`, `scannerapi`

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

### `sqlquery`

Executes a T-SQL query against a Fabric Lakehouse SQL endpoint and returns the parsed JSON payload.
Fab Inspector first resolves the SQL endpoint from the Lakehouse REST API (`GET /v1/workspaces/{workspaceId}/lakehouses/{lakehouseId}`), then executes the query with ADO.NET.

If your query does not include `FOR JSON`, Fab Inspector automatically appends `FOR JSON PATH`.

For safety, `sqlquery` only allows single SELECT-style queries and rejects SQL comments (`--`, `/* */`) and semicolons. It also blocks schema-changing statements (`CREATE`, `ALTER`, `DROP`).

**Two forms:**

| Form | When to use |
|---|---|
| Simple string | Uses `{context-fabricworkspace}` and `{context-fabricitem}` |
| Array | Explicit workspace and lakehouse GUIDs, with optional metadata refresh settings |

| Parameter | Type | Description |
|---|---|---|
| query | string | T-SQL query expression |
| workspaceId | string | Workspace GUID (or omit to use `{context-fabricworkspace}`) |
| lakehouseId | string | Lakehouse GUID (or omit to use `{context-fabricitem}`) |
| refreshMetadata | boolean | Refresh the Lakehouse SQL endpoint metadata before running the query (optional, default `false`) |
| recreateTables | boolean | When refreshing metadata, recreate SQL endpoint tables as part of the refresh request (optional, default `false`) |

`recreateTables` is only meaningful when `refreshMetadata` is `true`.

**Returns:** Parsed JSON payload from the SQL query result.

```json
{ "sqlquery": "SELECT TOP (5) [Country] FROM [dbo].[Customers]" }
```

```json
{
  "sqlquery": [
    "SELECT TOP (5) [Country] FROM [dbo].[Customers] FOR JSON PATH",
    "f45498e6-9f62-4bbb-bdb6-6d8a7e3a2703",
    "6a496a15-d00c-4cd6-a731-a3fd79e8fb10",
    true,
    false
  ]
}
```

See also: [Example-sqlquery-rule.json](Example-sqlquery-rule.json), [Example-sqlquery-wparams-rule.json](Example-sqlquery-wparams-rule.json)

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

*For authentication configuration, CLI parameters, and advanced usage see the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki) and the [CLI documentation](../README.md#cli).*
