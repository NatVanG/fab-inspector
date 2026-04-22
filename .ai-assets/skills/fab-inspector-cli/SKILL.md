| name | fab-inspector-cli |
|------|-------------------|
| description | Use the Fab Inspector CLI (`fab-inspector`) to run deterministic, rules-based inspections on Microsoft Fabric items. Activate when users need to invoke the CLI, configure authentication, set up CI/CD pipelines, or troubleshoot CLI parameters. |

# Fab Inspector CLI

This skill defines the complete CLI reference for the Fab Inspector command-line tool (`fab-inspector`), a deterministic rules-based testing tool for Microsoft Fabric items.

## 1 — What is Fab Inspector

Fab Inspector applies JSONLogic-based rules to Microsoft Fabric item definitions (Power BI Reports, CopyJobs, Lakehouses, Semantic Models, Data Pipelines, etc.) and reports violations. It works against:

- **Local** Fabric item definitions (files on disk, Git checkout)
- **Remote** items in a Fabric workspace (via REST API)
- **Hybrid** combinations of both

Output formats include console text, HTML reports, JSON results, PNG wireframes, and CI/CD-native annotations (Azure DevOps, GitHub Actions).

## 2 — Invocation

```
fab-inspector -fabricitem <path|guid> -rules <path|url> [options]
```

Or for workspace-scoped inspection (all items):

```
fab-inspector -fabricworkspace <guid> -rules <path|url> [options]
```

Or for item-scoped workspace inspection:

```
fab-inspector -fabricworkspace <guid> -fabricitem <guid> -rules <path|url> [options]
```

## 3 — Complete parameter reference

### Required parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `-fabricitem` | `<path\|guid>` | Local path to folder containing Fabric item definitions **or** Fabric item GUID when used with `-fabricworkspace`. Omit to inspect all items in the workspace. |
| `-rules` | `<path\|url>` | Path to a rules JSON file, or an OneLake DFS URL (requires non-local auth). |

### Alternative input (one of)

| Parameter | Value | Description |
|-----------|-------|-------------|
| `-fabricworkspace` | `<guid>` | Microsoft Fabric workspace ID (GUID). Requires non-local authentication. Enables remote file system access. |
| `-pbipreport` | `<path>` | **DEPRECATED**. Legacy path to a .pbip report file. |
| `-pbip` | `<path>` | **DEPRECATED**. Legacy path to a .pbip file. |

### Optional parameters

| Parameter | Value | Default | Description |
|-----------|-------|---------|-------------|
| `-output` | `<path\|url>` | Temp directory (deleted on exit) | Local directory path or OneLake DFS folder URL for output. OneLake URLs require non-local auth. |
| `-formats` | `<list>` | `CONSOLE` | Comma-, semicolon-, or pipe-separated list of: `CONSOLE`, `HTML`, `JSON`, `PNG`, `ADO`, `GitHub`. When `ADO` or `GitHub` is specified, other formats are ignored. |
| `-verbose` | `true\|false` | `false` | `true`: show all results including passes. `false`: show only violations. |
| `-parallel` | `true\|false` | `false` | `true`: split rules across available CPU cores. **Not supported with remote auth methods.** |
| `-overwriteoutput` | `true\|false` | `false` | `true`: overwrite existing output artifacts. `false`: preserve existing files. |

### Authentication parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `-authmethod` | `local\|interactive\|azurecli\|clientsecret\|certificate\|federatedtoken\|managedidentity` | Authentication method. Default: `local`. |
| `-tenantid` | `<guid>` | Azure AD tenant ID. Required for `clientsecret`, `certificate`, `federatedtoken`; optional for `azurecli` (tenant pinning). |
| `-clientid` | `<guid>` | Application (client) ID. Required for `clientsecret`, `certificate`, `federatedtoken`. Optional for `interactive` and `managedidentity` (user-assigned). |
| `-clientsecret` | `<string>` | Client secret. Required for `clientsecret` auth. |
| `-certificatepath` | `<path>` | Path to certificate file (.pem, .p12). Required for `certificate` auth. |
| `-certificatepassword` | `<string>` | Certificate password. Optional for `certificate` auth. |
| `-federatedtoken` | `<token>` | Federated identity token. Required for `federatedtoken` auth (e.g., GitHub OIDC). |

## 4 — Authentication methods

### local (default)
No additional parameters required. Uses only the local file system. Cannot access Fabric workspaces or OneLake URLs or APIs.

### interactive
Opens a browser-based authentication flow. Optionally provide `-clientid`.

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" -authmethod interactive
```

### azurecli
Developer flow using Azure CLI credentials. Requires prior `az login` command. Uses the Azure CLI token cache via `AzureCliCredential`. Optionally provide `-tenantid` to pin token acquisition to a specific tenant.

```bash
# Standard developer flow
az login
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" -authmethod azurecli

# With tenant pinning
az login
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" -authmethod azurecli -tenantid "<tenant-id>"
```

### clientsecret
Service principal with secret. Recommended for CI/CD pipelines. Requires `-tenantid`, `-clientid`, `-clientsecret`. Environment variables supported: `FABRIC_TENANT_ID`, `FABRIC_CLIENT_ID`, `FABRIC_CLIENT_SECRET`.

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" \
  -authmethod clientsecret -tenantid "<tid>" -clientid "<cid>" -clientsecret "<secret>"
```

### certificate
Service principal with certificate. Requires `-tenantid`, `-clientid`, `-certificatepath`. Optionally provide `-certificatepassword`.

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" \
  -authmethod certificate -tenantid "<tid>" -clientid "<cid>" -certificatepath "./cert.pem"
```

### federatedtoken
Federated identity credential (e.g., GitHub OIDC). Requires `-tenantid`, `-clientid`, `-federatedtoken`.

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" \
  -authmethod federatedtoken -tenantid "<tid>" -clientid "<cid>" \
  -federatedtoken "$ACTIONS_ID_TOKEN_REQUEST_TOKEN" -formats "GitHub"
```

### managedidentity
Azure Managed Identity. Optionally provide `-clientid` for user-assigned identity; omit for system-assigned.

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\rules.json" -authmethod managedidentity
```

## 5 — Output formats

| Format | Description |
|--------|-------------|
| `CONSOLE` | Default. Text output to stdout. |
| `HTML` | Rich HTML report with visual wireframes. |
| `JSON` | Machine-readable JSON results. Ideal for downstream processing. |
| `PNG` | Report page wireframe images. |
| `ADO` | Azure DevOps native log commands (`##vso[task.logissue]`). When specified, other formats are ignored. |
| `GitHub` | GitHub Actions annotations (`::error`, `::warning`). When specified, other formats are ignored. |

Multiple formats can be combined (except `ADO` and `GitHub` which are exclusive):

```bash
fab-inspector -fabricitem ".\FabricProject" -rules ".\rules.json" -formats "JSON,HTML,CONSOLE"
```

## 6 — Validation constraints

1. **At least one input** must be provided: `-fabricitem`, `-fabricworkspace`, `-pbipreport`, or `-pbip`.
2. **`-rules` is always required.**
3. **`-fabricworkspace` value must be a valid GUID.**
4. When `-fabricworkspace` is used with `-fabricitem`, the item value **must also be a valid GUID** (item-scoped mode).
5. **OneLake URLs** for `-rules` or `-output` require a non-local authentication method.
6. **`-parallel true` is not supported** with remote authentication methods (workspace mode). Local auth only.
7. **`clientsecret` auth** requires all three: `-tenantid`, `-clientid`, `-clientsecret`.
8. **`certificate` auth** requires: `-tenantid`, `-clientid`, `-certificatepath`.
9. **`federatedtoken` auth** requires: `-tenantid`, `-clientid`, `-federatedtoken`.
10. **`azurecli` auth** requires prior `az login`; optionally provide `-tenantid` for tenant pinning.

## 7 — Common invocation examples

### Local-only (development)

```bash
fab-inspector -fabricitem "C:\FabricProject" -rules "C:\Rules\MyRules.json" -output "C:\FabResults" -formats "JSON,HTML"
```

### CI/CD — Azure DevOps

```bash
fab-inspector -fabricitem "./FabricProject" -rules "./Rules/ci-rules.json" -formats "ADO"
```

### CI/CD — GitHub Actions with OIDC

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules "./Rules/ci-rules.json" \
  -authmethod federatedtoken -clientid "<cid>" -tenantid "<tid>" \
  -federatedtoken "$ACTIONS_ID_TOKEN_REQUEST_TOKEN" -formats "GitHub"
```

### Workspace-scoped (interactive)

```bash
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\Base-rules.json" -authmethod interactive -formats "JSON,HTML"
```

### Workspace-scoped with Azure CLI (developer flow)

```bash
az login
fab-inspector -fabricworkspace "<ws-guid>" -rules ".\Base-rules.json" -authmethod azurecli -formats "JSON,HTML"
```

### Workspace-scoped with OneLake rules and output

```bash
fab-inspector -fabricworkspace "<ws-guid>" \
  -rules "https://onelake.dfs.fabric.microsoft.com/<ws>/<lh>/Files/rules/rules.json" \
  -authmethod clientsecret -clientid "<cid>" -tenantid "<tid>" -clientsecret "<secret>" \
  -output "https://onelake.dfs.fabric.microsoft.com/<ws>/<lh>/Files/results" -formats "JSON"
```

### Item-scoped workspace

```bash
fab-inspector -fabricworkspace "<ws-guid>" -fabricitem "<item-guid>" \
  -rules ".\Base-rules.json" -authmethod interactive -formats "Console"
```

### Parallel local execution

```bash
fab-inspector -fabricitem ".\FabricProject" -rules ".\rules.json" -parallel true -formats "JSON"
```

### Docker (GitHub Actions runner)

See the [example GitHub Actions workflow](https://github.com/NatVanG/fab-inspector-cicd-example/blob/main/.github/workflows/fab-inspector.yml) for running via the published `fab-inspector` Docker image on an Ubuntu runner.

## 8 — Sensitive data handling

- **Never log or output** tokens, passwords, client secrets, or federated tokens.
- Service principal credentials can be passed via environment variables (`FABRIC_TENANT_ID`, `FABRIC_CLIENT_ID`, `FABRIC_CLIENT_SECRET`) instead of command-line arguments.
- **Azure CLI credentials** (`azurecli` auth) are managed by Azure CLI/MSAL token storage; no client secrets are passed on the command line.
- If a user shares sensitive strings, advise rotating/regenerating them.
- When suggesting commands that include `-clientsecret` or `-federatedtoken`, use placeholder values and note the security implications.

## 9 — Registered operators

The CLI registers the following custom JSONLogic operators at startup. These are available for use in rule definitions. See the **fab-inspector-rules** skill for full operator documentation.

### Ric operators (general-purpose)
`count`, `drillvar`, `filesize`, `filetextsearchcount`, `isnullorempty`, `partinfo`, `part`, `path`, `query`, `diff`, `equalsets`, `intersection`, `symdiff`, `union`, `strcontains`, `torecord`, `tostring`, `fromyamlfile`, `keys`, `values`, `distinct`, `typeof`, `hasprop`, `strsplit`, `strjoin`, `regexextract`, `coalesce`, `slice`, `now`, `datediff`, `let`

### Fab Inspector operators (Fabric-specific)
`rectoverlap`, `daxquery`, `apiget`, `dfsget`, `scannerapi`

## 10 — Help

```bash
fab-inspector -help
fab-inspector --help
fab-inspector /?
```

## 11 — Cross-references

- **Rule authoring**: see the `fab-inspector-rules` skill in this repository
- **Wiki**: https://github.com/NatVanG/PBI-InspectorV2/wiki
- **Example rules**: `DocsExamples/` folder in this repository
- **Base rules**: `Rules/Base-rules.json`






