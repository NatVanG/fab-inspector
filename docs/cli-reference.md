# Fab Inspector — CLI Reference

All command-line parameters for `fab-inspector`.

---

## Parameters

```-fabricitem folderpath|itemid```  
Path to a local CI/CD folder containing one or more Fabric item definitions (local mode), or a Fabric Item ID GUID when used with `-fabricworkspace` (Fabric mode). In local mode, Fab Inspector traverses subfolders so you can specify either a root folder or a specific subfolder. In Fabric workspace-scoped mode, omit this parameter to inspect all items in the workspace.

```-fabricworkspace workspaceid```  
Optional. Microsoft Fabric Workspace ID (GUID). When specified, enables Fabric mode where the Inspector uses the Fabric remote file system to access items directly from a Fabric workspace.
- **Workspace-scoped access**: Omit `-fabricitem` to inspect all items in the workspace.
- **Item-scoped access**: Provide a Fabric Item ID GUID via `-fabricitem` to inspect only that item.
- **Authentication required**: `-authmethod` must be one of `interactive`, `azurecli`, `clientsecret`, `certificate`, `federatedtoken`, or `managedidentity`.

```-rules filepath```  
Required. Path to the rules file. This can be a local JSON file path or a OneLake DFS URL. OneLake rules URLs require non-local authentication.

```-rulescatalog filepath```  
Alternative to `-rules`. Path to a rules catalog JSON file that references multiple rulesets. Mutually exclusive with `-rules`. See [Example-RulesCatalog.json](../ExampleRules/Example-RulesCatalog.json).

```-authmethod local|interactive|azurecli|clientsecret|certificate|federatedtoken|managedidentity```  
Optional, defaults to `local`.

| Value | Description |
|---|---|
| `local` (default) | No authentication, uses local file system. |
| `interactive` | Interactive browser authentication. |
| `azurecli` | Developer authentication using Azure CLI credentials from a prior `az login`. Optionally pair with `-tenantid` to pin to a specific tenant. |
| `clientsecret` | Service principal authentication using client secret. See [Handling client secrets safely](#handling-client-secrets-safely). |
| `certificate` | Service principal authentication using a certificate. |
| `federatedtoken` | Service principal authentication using federated token. |
| `managedidentity` | Managed identity authentication. |

```-clientid clientid```  
Optional. Azure AD application (client) ID. Required for `clientsecret`, `certificate`, and `federatedtoken`. Optional for `interactive` and `managedidentity`. Can also be provided via the `FABRIC_CLIENT_ID` environment variable.

```-tenantid tenantid```  
Optional. Azure AD tenant ID/name. Required for `clientsecret`, `certificate`, and `federatedtoken`; optional for `azurecli` tenant pinning. Can also be provided via `FABRIC_TENANT_ID`.

```-clientsecret secret```  
Optional. Required for `clientsecret`. Can also be provided via `FABRIC_CLIENT_SECRET`. See [Handling client secrets safely](#handling-client-secrets-safely).

```-certificatepath path```  
Optional. Required for `certificate`. Path to certificate file (`.pem`, `.p12`, etc.).

```-certificatepassword password```  
Optional. Certificate password (used with `certificate` when needed).

```-federatedtoken token```  
Optional. Required for `federatedtoken`.

```-output directorypath|onelakeurl```  
Optional. Output local directory path or OneLake folder URL. If omitted, a temporary local directory is created. OneLake output requires non-local authentication.

```-overwriteoutput true|false```  
Optional, false by default. If true, existing output artifacts can be overwritten.

```-formats CONSOLE,JSON,HTML,PNG,ADO,GitHub```  
Optional. Comma-separated list of output formats.

| Format | Description |
|---|---|
| `CONSOLE` (default) | Writes results to standard console output. Used when `-formats` is not specified. |
| `JSON` | Writes results to a JSON file. |
| `HTML` | Writes results to a formatted HTML page. If no output directory is specified and HTML is enabled, the page opens automatically. HTML output includes report page wireframe images so `PNG` is usually not needed in addition. |
| `PNG` | Writes report page wireframe images highlighting failing visuals. |
| `ADO` | Emits Azure DevOps task commands (`task.logissue`, `task.complete`) for pipeline integration. When `ADO` is specified, other output formats are ignored. |
| `GitHub` | Emits GitHub Actions-compatible logging/annotations. |

```-verbose true|false```  
Optional, false by default. If false then only rule violations are shown; if true then all results are listed.

```-parallel true|false```  
Optional, false by default. If true, rules are split across available processors and run in parallel before results are merged.

> **Warnings when using `-parallel true`:**
> - If rules use `applyPatch`, avoid parallel patching of the same part/file because writes can conflict and become last-writer-wins.
> - Rules that call remote APIs (`apiget`, `dfsget`, `daxquery`, `sqlquery`, `scannerapi`) may hit service throttling/rate limits sooner under parallel fan-out.
> - `scannerapi` polls for up to 5 minutes per request; parallel use with this operator is not recommended.

```-pbip filepath``` / ```-pbipreport filepath```  
Deprecated. Use `-fabricitem` instead. Targeting a `*.pbip` file still works for local mode.

```-pbix filepath```  
Not currently supported.

```-help``` (or ```--help``` or ```/?```)  
Displays all CLI options and short descriptions.

---

## Examples

**Local mode — single report:**
```bash
fab-inspector -fabricitem "C:\Files\Sales.Report" -rules ".\Files\Base-rules.json" -output "C:\Files\TestRun" -formats "Console,JSON"
```

**Local mode — console only:**
```bash
fab-inspector -fabricitem "C:\Files\Sales.Report" -rules ".\Files\Base-rules.json" -formats "Console"
```

**Local mode — Azure DevOps pipeline output:**
```bash
fab-inspector -fabricitem "C:\Files\Sales.Report" -rules ".\Files\Base-rules.json" -formats "ADO"
```

**Local mode — CopyJob item with GitHub logging:**
```bash
fab-inspector -fabricitem "C:\Files\copyjob1.CopyJob" -rules "C:\Files\Sample-CopyJob-Rules.json" -formats GitHub
```

**Workspace-scoped — all items, interactive auth:**
```bash
fab-inspector -fabricworkspace "12345678-1234-1234-1234-123456789abc" -rules ".\Files\Base-rules.json" -authmethod interactive -formats "Console,JSON"
```

**Workspace-scoped — all items, Azure CLI auth:**
```bash
fab-inspector -fabricworkspace "12345678-1234-1234-1234-123456789abc" -rules ".\Files\Base-rules.json" -authmethod azurecli -formats "Console"
```

**Item-scoped — single item, interactive auth:**
```bash
fab-inspector -fabricworkspace "12345678-1234-1234-1234-123456789abc" -fabricitem "87654321-4321-4321-4321-cba987654321" -rules ".\Files\Base-rules.json" -authmethod interactive -formats Console
```

**Item-scoped — CI/CD pipeline with client secret:**
```bash
fab-inspector -fabricworkspace "12345678-1234-1234-1234-123456789abc" -fabricitem "87654321-4321-4321-4321-cba987654321" -rules ".\Files\Base-rules.json" -authmethod clientsecret -clientid "your-client-id" -tenantid "your-tenant-id" -clientsecret "your-secret" -formats ADO
```

**GitHub Actions — federated token (OIDC) against workspace:**
```bash
fab-inspector -fabricworkspace "<workspace-guid>" -rules "./Rules/ci-rules.json" -authmethod federatedtoken -clientid "<client-id>" -tenantid "<tenant-id>" -federatedtoken "$ACTIONS_ID_TOKEN_REQUEST_TOKEN" -formats "GitHub"
```

**OneLake-hosted rules and results — client secret:**
```bash
fab-inspector -fabricworkspace "<workspace-guid>" -rules "https://onelake.dfs.fabric.microsoft.com/<workspace>/<lakehouse>/Files/rules/rules.json" -authmethod clientsecret -clientid "<client-id>" -tenantid "<tenant-id>" -clientsecret "<secret>" -output "https://onelake.dfs.fabric.microsoft.com/<workspace>/<lakehouse>/Files/results" -formats "JSON"
```

**Show all options:**
```bash
fab-inspector -help
```

---

## Handling client secrets safely

Treat client secrets as credentials, not configuration. Do not commit them to source control, paste real values into checked-in scripts, or expose them in build logs, screenshots, or shared chat threads.

Prefer CI/CD secret stores, environment variables, or platform-managed credential mechanisms over hard-coded command lines. If a secret may have been exposed, rotate it immediately and update any dependent pipeline or app configuration.

The following environment variables are read automatically when the corresponding flags are not provided:

| Variable | Flag |
|---|---|
| `FABRIC_CLIENT_ID` | `-clientid` |
| `FABRIC_TENANT_ID` | `-tenantid` |
| `FABRIC_CLIENT_SECRET` | `-clientsecret` |

---

*For CI/CD pipeline setup and tutorials see [Azure DevOps and GitHub integration](usage-scenarios.md). For operator reference see [Ric Operators](Ric-Operators.md) and [FabInspector Operators](FabInspector-Operators.md).*
