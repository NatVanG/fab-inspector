# FabInspector Operators — Quick Reference

> For in-depth explanations and advanced examples, see the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki).

FabInspector operators extend the [JSON Logic](https://json-everything.net/json-logic) engine with remote API access and layout analysis. They are available in the `test` field of any rule and require a non-`local` authentication method.

For a guide on when to use FabInspector and Ric operators, see [Operators Overview](../docs/operators-overview.md).

**Authentication:** All REST API operators require a non-`local` authentication method (e.g. `interactive`, `clientsecret`, `certificate`, `federatedtoken`, or `managedidentity`). See the [CLI Reference](../docs/cli-reference.md#parameters) for authentication options.

**URL placeholder tokens** are automatically resolved at runtime:

| Token | Resolved to |
|---|---|
| `{context-fabricworkspace}` | The workspace ID from the `-fabricworkspace` CLI parameter |
| `{context-fabricitem}` | The item ID from the `-fabricitem` CLI parameter |

---

## Contents

- [REST API Operators](#rest-api-operators): `apiget`, `dfsget`, `daxquery`, `sqlquery`, `scannerapi`
- [Permission Guidance](#permission-guidance)

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
{
  "apiget": [
    "https://api.fabric.microsoft.com/v1/workspaces/{fabricworkspace}/items?type={type}&recursive={recursive}",
    "f45498e6-9f62-4bbb-bdb6-6d8a7e3a2703",
    "Lakehouse",
    "true"
  ]
}
```

```json
{ "apiget": ["https://api.fabric.microsoft.com/v1/workspaces/{context-fabricworkspace}/onelake/settings"] }
```

See also: [18-PBI-apiget-Rules.json](../ExampleRules/18-PBI-apiget-Rules.json), [08-Fabric-apiget-Rules.json](../ExampleRules/08-Fabric-apiget-Rules.json)

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

See also: [05-dfsget-Rules.json](../ExampleRules/05-dfsget-Rules.json)

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

See also: [01-daxquery-Rules.json](../ExampleRules/01-daxquery-Rules.json)

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

See also: [26-sqlquery-Rules.json](../ExampleRules/26-sqlquery-Rules.json)

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

See also: [25-scannerapi-Rules.json](../ExampleRules/25-scannerapi-Rules.json)

---


:Note: All REST API operators require a non-`local` authentication method.

## Permission Guidance

The required permission depends on the operator and, for `apiget`, the specific endpoint you call.

Fab Inspector is intended to be used as a read-only inspection tool. Follow least-privilege principles by granting only the minimum workspace roles, item permissions, and delegated scopes required for the specific rule/API call.

| Operator | Required permission guidance | Microsoft Learn references |
|---|---|---|
| `apiget` | Depends on the target API. For Fabric REST endpoints, use the endpoint's **Permissions** and **Required Delegated Scopes** sections. For Power BI endpoints with service principal auth, enable tenant setting **Allow service principals to use Power BI APIs** and grant workspace/item permissions required by that endpoint. | [Fabric scopes](https://learn.microsoft.com/en-us/rest/api/fabric/articles/scopes), [List Items (example: viewer role + Workspace.Read.All/Workspace.ReadWrite.All)](https://learn.microsoft.com/en-us/rest/api/fabric/core/items/list-items), [Using Power BI REST APIs (service principal and scopes)](https://learn.microsoft.com/en-us/rest/api/power-bi/#scopes), [Enable service principal for Power BI APIs](https://learn.microsoft.com/en-us/power-bi/developer/embedded/embed-service-principal#step-3---enable-the-power-bi-service-admin-settings) |
| `dfsget` | Use a Microsoft Entra bearer token for OneLake DFS. The caller must have permission to read the target OneLake path/item in Fabric. | [Connecting to OneLake - Authorization](https://learn.microsoft.com/en-us/fabric/onelake/onelake-access-api#authorization), [OneLake access via API](https://learn.microsoft.com/en-us/fabric/onelake/onelake-access-api) |
| `daxquery` | Tenant setting **Dataset Execute Queries REST API** must be enabled. Caller needs semantic model (dataset) **Read** and **Build** permission. Required scope is **Dataset.Read.All** or **Dataset.ReadWrite.All**. If using service principal, enable **Allow service principals to use Power BI APIs**. | [Datasets - Execute Queries](https://learn.microsoft.com/en-us/rest/api/power-bi/datasets/execute-queries), [Dataset access permissions](https://learn.microsoft.com/en-us/power-bi/connect-data/service-datasets-manage-access-permissions), [Allow service principals to use Power BI APIs](https://learn.microsoft.com/en-us/power-bi/admin/service-admin-portal-developer#allow-service-principals-to-use-power-bi-apis) |
| `sqlquery` | Fab Inspector resolves Lakehouse SQL endpoint via `GET /v1/workspaces/{workspaceId}/lakehouses/{lakehouseId}` first. Caller must have **read** permission on the lakehouse. Required delegated scopes for this step are **Lakehouse.Read.All**, **Lakehouse.ReadWrite.All**, **Item.Read.All**, or **Item.ReadWrite.All**. Caller must also be permitted to read/query SQL endpoint objects. | [Items - Get Lakehouse](https://learn.microsoft.com/en-us/rest/api/fabric/lakehouse/items/get-lakehouse), [Lakehouse REST API overview](https://learn.microsoft.com/en-us/fabric/data-engineering/lakehouse-api), [Microsoft Entra auth for Fabric SQL endpoints](https://learn.microsoft.com/en-us/fabric/data-warehouse/entra-id-authentication) |
| `scannerapi` | Calls a Power BI **admin** API (`PostWorkspaceInfo`). User must be a Fabric admin, or use service principal auth for admin APIs. With delegated admin token, required scope is **Tenant.Read.All** or **Tenant.ReadWrite.All**. With service principal auth, the app must not have admin-consent required Power BI permissions configured for this call path. | [WorkspaceInfo - PostWorkspaceInfo](https://learn.microsoft.com/en-us/rest/api/power-bi/admin/workspace-info-post-workspace-info), [Enable service principal authentication for admin APIs](https://learn.microsoft.com/en-us/fabric/admin/enable-service-principal-admin-apis), [Admin API tenant settings](https://learn.microsoft.com/en-us/fabric/admin/service-admin-portal-admin-api-settings) |

---

*For authentication configuration, CLI parameters, and advanced usage see the [Fab Inspector wiki](https://github.com/NatVanG/PBI-InspectorV2/wiki) and the [CLI Reference](../docs/cli-reference.md).*
