# FabInspector Workload (React)

React + TypeScript frontend for the FabInspector Microsoft Fabric custom workload,
built on the [Fabric Extensibility Toolkit](https://learn.microsoft.com/en-us/fabric/extensibility-toolkit/extensibility-toolkit-overview).

## Layout

- `app/` — React source (TypeScript, Fluent UI v9, `@ms-fabric/workload-client`).
  - `clients/` — typed `fetch` wrappers around the .NET `/api/workload/*` endpoints.
  - `controller/` — wrappers around the Fabric host (`itemCrud`, `itemSchedule`, `notification`, theme bridge).
  - `items/` — one folder per custom item type.
  - `types/` — TypeScript types mirroring `FabInspector.Web/Workload/Contracts/*.cs`.
- `Manifest/` — Fabric workload manifest templates (committed) processed at build time.
- `scripts/` — PowerShell setup / build helpers adapted from the toolkit starter kit.
- `.env.{dev,test,prod,template}` — environment-specific configuration.

The .NET backend (`FabInspector.Web`) hosts the lifecycle/job controllers
(`/api/workload/items/...`, `/api/workload/jobs/...`) and is unchanged.

## Local development

```pwsh
# 1. Install dependencies (npm install + dotnet restore)
pwsh ./scripts/Setup/SetupDevEnvironment.ps1

# 2. Start the backend (separate terminal) — https://localhost:7095
pwsh ./scripts/Run/StartBackend.ps1

# 3. Start the React dev server — https://localhost:60006 (proxies /api → backend)
pwsh ./scripts/Run/StartDevServer.ps1

# 4. Register the workload with Fabric via DevGateway (see scripts/Run/StartDevGateway.ps1)
```

The dev server runs on `https://localhost:60006`, matching the
Extensibility Toolkit DevGateway convention. The backend listens on
`https://localhost:7095` — both ports are configured in `.env.dev` and
`FabInspector.Web/Properties/launchSettings.json`.

## Build for deployment

```pwsh
pwsh ./scripts/Build/BuildFrontend.ps1   # → ../build/Frontend
pwsh ./scripts/Build/BuildAll.ps1        # .NET solution + frontend bundle
```

## CI

`.github/workflows/workload-ci.yml` runs on changes to `Workload/**` or
`FabInspector.Web/**`: type-checks and builds the React bundle, runs vitest,
and builds the .NET Web project against the .NET 8 SDK.
