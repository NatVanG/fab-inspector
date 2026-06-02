# FabInspector.Web

ASP.NET Core (.NET 8) backend for FabInspector's Microsoft Fabric custom workload.

This service hosts:

- Fabric workload lifecycle endpoints for custom item types (rule sets and rule catalogs)
- Fabric workload job endpoints to execute inspections asynchronously
- Authentication/authorization for both Fabric SubjectAndAppToken and interactive OIDC sign-in
- Token acquisition for downstream Fabric, Power BI, and OneLake APIs
- Optional durable job telemetry in Azure Table Storage

It works with the React frontend in Workload/ and the shared inspection engine from FabInspector.Core and FabInspector.ClientLibrary.

## Project layout

- Program.cs: DI, auth schemes, CORS, middleware, endpoint mapping
- Controllers/ItemLifecycleController.cs: item lifecycle + editor-facing item-definition GET
- Controllers/JobActionController.cs: start/status/cancel job actions
- Workload/Auth/SubjectAndAppTokenAuthHandler.cs: validates Fabric SubjectAndAppToken1.0 requests
- Auth/DelegatedTokenProvider.cs: delegated/OBO downstream token acquisition
- Workload/Runtime/WorkloadInspectionService.cs: orchestrates one inspection run per job instance
- Workload/Stores/FabricItemDefinitionStore.cs: Fabric REST-backed item-definition reads with memory cache
- Workload/Jobs/JobRunStore.cs: in-memory job store
- Workload/Jobs/AzureTableJobRunStore.cs: durable Azure Table job store

## Runtime requirements

- .NET SDK 8.0+
- An Entra app registration for this backend (AzureAd + Workload:Auth:WorkloadAppId)
- Consent/scopes for downstream APIs used by inspections:
  - https://analysis.windows.net/powerbi/api/.default
  - https://api.fabric.microsoft.com/.default
  - https://storage.azure.com/.default
- Optional: Azure Storage Table endpoint or connection string if using Workload:Jobs:Store = AzureTable

## Authentication model

Two authentication schemes are registered:

1. OpenIdConnect + Microsoft.Identity.Web
- Used for interactive sign-in and sign-out
- Enables silent downstream token acquisition from MSAL cache

2. SubjectAndAppToken1.0 (custom handler)
- Used by Fabric workload lifecycle and job callbacks
- Validates appToken and subjectToken signatures using OIDC JWKS
- Verifies audience, issuer, app identity, and required subject scope (default FabricWorkloadControl)

Fallback authorization requires an authenticated principal unless explicitly opted out.

## Configuration

Primary settings are in appsettings.json.

### AzureAd

- Instance
- Domain
- TenantId
- ClientId
- CallbackPath
- SignedOutCallbackPath

### Workload:Auth

- WorkloadAppId: audience app id for incoming workload tokens (required outside local dev bypass)
- FabricAppId: Fabric service app id (override for sovereign clouds if needed)
- Authority: OIDC authority used to fetch JWKS
- AllowedTenants: optional tenant allow list
- RequiredSubjectScope: expected subject token scope (default FabricWorkloadControl)
- AllowAnonymousInDevelopment: dev-only escape hatch (only honored when ASPNETCORE_ENVIRONMENT=Development)

### Workload:Items

- Store: Fabric (default, production) or InMemory (local/dev)
- CacheTtlSeconds: memory cache TTL for definition reads
- LongRunningTimeoutSeconds: timeout for Fabric long-running operations
- LongRunningPollSeconds: fallback poll interval for LRO status polling
- FabricApiBaseUrl: Fabric REST base URL

### Workload:Jobs

- Store: InMemory (default) or AzureTable
- ConnectionString: Table Storage connection string (optional if AccountUri used)
- AccountUri: Table endpoint URI (with DefaultAzureCredential)
- RunsTableName: run metadata table name
- LogsTableName: log chunks table name
- RetentionDays: retention horizon for run data
- RetentionScanIntervalMinutes: cleanup interval

### Cors

- AllowedOrigins: allowed frontend origins (defaults include https://localhost:60006 and http://localhost:60006)

## Local development

From repository root:

```powershell
# Restore/build all projects
dotnet restore FabInspector.sln
dotnet build FabInspector.sln
```

From this project folder:

```powershell
# Run backend with HTTPS profile from launchSettings.json
dotnet run --launch-profile https
```

Default development URLs from Properties/launchSettings.json:

- https://localhost:7095
- http://localhost:5246

If working with the React workload frontend, follow Workload/README.md for coordinated backend + dev server startup.

## API surface

### Item lifecycle routes

Base route:

- /api/workload/items/{itemType}/{workspaceId}/{itemId}

Supported operations:

- POST: create item definition
- PATCH/PUT: update item definition (If-Match supported)
- DELETE: delete item (DeleteItemRequest deleteType = Hard|Soft)
- POST /restore: restore item
- GET: read cached/current definition for editor hydration

Known item types:

- FabInspectorRuleSet (expects rules.json part)
- FabInspectorRulesCatalog (expects catalog.json part)

### Job action routes

Base route:

- /api/workload/jobs/{itemType}/{workspaceId}/{itemId}/{jobType}/jobInstances/{jobInstanceId}

Supported operations:

- POST: start a job instance
- GET: get job status
- POST /cancel: request cancellation

Known job types:

- FabInspector.FabInspectorRuleSet.RunRules
- FabInspector.FabInspectorRulesCatalog.RunCatalog

Status values:

- NotStarted
- InProgress
- Succeeded
- Failed
- Cancelled

Responses include Fabric-compatible status fields plus a non-standard fabInspector object with:

- passCount
- failCount
- log

## Job execution flow

1. JobActionController creates a job run record.
2. WorkloadInspectionService runs in background for that job instance.
3. ItemDefinitionResolver resolves the source definition:
- RunRules: decode rules.json from one rule-set item
- RunCatalog: decode catalog.json, resolve pointers to rule sets, merge rules
4. Rules are written to a temp JSON file for the existing path-based inspection engine.
5. InspectionRunner executes FabInspector engine and updates pass/fail/log fields.
6. Terminal state is persisted in job store; durable mode also stores chunked logs in Azure Table.

## Error contract

Workload endpoints return structured errors using Workload/Contracts/ErrorResponse.cs.

Common errorCode values:

- UnknownItemType
- InvalidDefinition
- InvalidPayload
- Unauthenticated
- Forbidden
- ConsentRequired
- ETagMismatch
- Conflict
- NotFound
- Internal

ConsentRequired errors return 403 and may include a WWW-Authenticate Bearer claims challenge for interactive re-consent.

## Durable telemetry with Azure Table

When Workload:Jobs:Store = AzureTable:

- Two tables are used (runs and logs)
- Tables are created automatically at startup if missing
- Active jobs are cached in-process to preserve cancellation token behavior
- Terminal logs are chunked and stored in logs table due Table entity size limits
- Background retention service removes old run/log rows

## Production notes

- Keep AllowAnonymousInDevelopment = false in non-development environments.
- Set Workload:Auth:WorkloadAppId and tenant/authority values explicitly.
- Prefer AzureTable job store for multi-instance deployments.
- Configure CORS AllowedOrigins to exact trusted frontend origins.
- Use managed identity + AccountUri for Table Storage where possible.

## Related docs

- Workload frontend guide: ../Workload/README.md
- Solution root overview: ../README.md
