# FabInspector Core Architecture

This document explains the inspection execution model after the Dependency Injection refactor, with emphasis on:

- Per-run `InspectionEngine` isolation
- Ambient `InspectionContext` flow through `InspectionContextHolder`
- The DI composition hook `ServiceCollectionExtensions.AddFabInspectorCore`

The isolation boundary is the inspection request or run: mutable engine and ambient context state stay isolated, while the run-level `IFabricFileSystem` is reused within that request so item-definition downloads can be cached across rulesets and parallel buckets.

## 1. Runtime components

- `FabInspector.ClientLibrary.Hosting.ServiceCollectionExtensions.AddFabInspectorCore`
  - Registers process-wide collaborators (currently singleton `HttpClient`).
- `FabInspector.ClientLibrary.InspectionEngine`
  - Per-run coordinator.
  - Pushes ambient `InspectionContext` for the run.
  - Creates and reuses one `IFabricFileSystem` per run so workspace item definitions can be cached across rulesets and parallel buckets.
  - Splits rule execution into single-thread or parallel pathways.
- `FabInspector.Core.Inspection.InspectionContext`
  - Single POCO carrying run-level and per-rule/per-part mutable fields.
- `FabInspector.Core.Inspection.InspectionContextHolder`
  - Single `AsyncLocal<InspectionContext?>` slot with `PushScope(...)`, `Current`, `Require(...)`.
- `FabInspector.Core.Inspector`
  - Executes rule traversal and mutates context fields (`RuleName`, `Part`, `ItemPath`, `FabricItem`) as traversal advances.

## 2. DI composition and object lifetime

```mermaid
flowchart LR
  A["Host startup<br/>CLI WinForm Web"] --> B["Create ServiceCollection"]
  B --> C["Call AddFabInspectorCore"]
  C --> D["Register singleton HttpClient"]
  D --> E["Build ServiceProvider"]

  E --> F["Inspection request arrives"]
  F --> G["Resolve or reuse singleton HttpClient"]
  G --> H["Construct new InspectionEngine with HttpClient"]
  H --> I["Resolve rulesets and create one shared IFabricFileSystem per run"]
  I --> J["RunAsync or RunAndReturnResultsAsync"]

  classDef single fill:#e8f5e9,stroke:#2e7d32,stroke-width:1px,color:#000
  classDef perrun fill:#fff8e1,stroke:#f9a825,stroke-width:1px,color:#000
  class D single
  class H,I,J perrun
```

Notes:

- `HttpClient` is intentionally singleton and shared process-wide.
- `InspectionEngine` is intentionally transient/per-run.
- Hosts can execute concurrent runs safely by creating separate engine instances.

## 3. End-to-end inspection run flow

```mermaid
sequenceDiagram
    participant Host
    participant Engine as InspectionEngine
    participant Holder as InspectionContextHolder
    participant Loader as RulesFileLoader/Catalog
    participant FS as IFabricFileSystem
    participant Insp as Inspector
    participant Ops as Json.Logic Operators

    Host->>Engine: RunAsync(args, renderer, registries)
    Engine->>Engine: AuthenticateAsync(...) if AuthMethod != local
    Engine->>Engine: Build InspectionContext\n(HttpClient, workspace/item, TokenProvider)
    Engine->>Holder: PushScope(context)
    Holder-->>Engine: IDisposable scope

    Engine->>Loader: ResolveRuleSetsAsync(...)
    Loader-->>Engine: Resolved rulesets
    Engine->>FS: CreateFileSystemAsync(...) once per run
    Engine->>FS: Set ScopedItemTypes from all resolved rulesets

    loop each ruleset
        Engine->>Engine: ExecuteSingleRuleSetAsync(...)
        Engine->>Insp: new Inspector(rules, registries, fileSystem)
        Engine->>Insp: Inspect()
        Insp->>Holder: read/write Current context fields
        Insp->>Ops: evaluate JsonLogic operators
        Ops->>Holder: Require(...) / Current
        Ops-->>Insp: operator result
        Insp-->>Engine: TestResult[]
    end

    Engine->>Holder: Dispose run scope
    Engine-->>Host: TestRun / output stream
```

## 4. Concurrent flow and per-task ambient cloning

When `args.Parallel == true`, `InspectionEngine.ExecuteSingleRuleSetAsync(...)` chunks rules and runs buckets concurrently.

Critical detail:

- A single shared ambient context would race because `Inspector` mutates fields like `RuleName`, `Part`, `ItemPath`, and `FabricItem`.
- To prevent this, each parallel task creates `perTaskContext = ambient with { }` and pushes it via `InspectionContextHolder.PushScope(...)`.
- The run-level `IFabricFileSystem` is intentionally shared because its remote item-definition cache is thread-safe and should serve all buckets in the same run.

```mermaid
flowchart TB
  A["Run scope pushed once<br/>ambient context C0"] --> B{"Parallel"}
  B -- No --> C["RunSingleThreadedAsync<br/>mutates C0 safely and reuses the shared filesystem"]
  B -- Yes --> D["Chunk rules into buckets"]
  D --> E1["Task 1 clone C1 from C0<br/>PushScope C1"]
  D --> E2["Task 2 clone C2 from C0<br/>PushScope C2"]
  D --> E3["Task N clone CN from C0<br/>PushScope CN"]

  E1 --> F1["Inspector mutates C1 fields"]
  E2 --> F2["Inspector mutates C2 fields"]
  E3 --> F3["Inspector mutates CN fields"]

  FS["Shared run-level IFabricFileSystem<br/>cached definitions and workspace lookups"]
  FS --> F1
  FS --> F2
  FS --> F3

  F1 --> G["Task scope disposed"]
    F2 --> G
    F3 --> G
  G --> H["Merge bucket results"]

  classDef safe fill:#e3f2fd,stroke:#1565c0,stroke-width:1px,color:#000
  class E1,E2,E3,F1,F2,F3 safe
  class FS safe
```

## 5. Ambient context semantics

`InspectionContextHolder` behaves like a stack per async flow:

```mermaid
flowchart LR
  A["Current is null"] --> B["PushScope ctxA"]
  B --> C["Current is ctxA"]
  C --> D["PushScope ctxB"]
  D --> E["Current is ctxB"]
  E --> F["Dispose inner scope"]
  F --> G["Current restored to ctxA"]
  G --> H["Dispose outer scope"]
  H --> I["Current is null"]
```

Operational rules:

- Operators that require context call `InspectionContextHolder.Require(operatorName)` and fail fast with a clear message when missing.
- Operator progress flows through `InspectionContextHolder.ReportOperatorProgress(...)`, which formats messages using ambient `RuleName`, `Part`, and `ItemPath`.
- `Inspector` may create a local fallback scope only when invoked without a parent scope (legacy direct-instantiation paths), but remote operators still fail if token/host state is unavailable.

## 6. Mutation map (what changes during a run)

Stable for the run (typically set once by `InspectionEngine`):

- `InspectionContext.HttpClient`
- `InspectionContext.FabricWorkspaceId`
- `InspectionContext.TokenProvider`

Mutated during traversal:

- `InspectionContext.FabricItem` (updated per discovered item in workspace-scoped iteration)
- `InspectionContext.RuleName`
- `InspectionContext.PartQuery`
- `InspectionContext.Part`
- `InspectionContext.ItemPath`
- `InspectionContext.MessageReporter`

Because those mutable fields are evaluation-local, parallel paths must not share a single mutable context instance.

## 7. Compatibility path

- `Main` remains a static facade for CLI/WinForm/MCP compatibility.
- Each static entrypoint still constructs a fresh `InspectionEngine` per call.
- The static event (`WinMessageIssued`) is a forwarding surface from per-engine `MessageIssued` events.

This keeps existing host code working while preserving per-run isolation guarantees.

## 8. Multi-user web backend isolation model

In a multi-user web host (for example ASP.NET Core / Blazor Server), the isolation boundary is the inspection request.
Each request constructs a new `InspectionEngine` and gets its own ambient `InspectionContext` scope.

Recommended host behavior:

- Use `AddFabInspectorCore()` once at startup for process-wide registrations.
- Resolve/reuse singleton `HttpClient` from DI.
- Create a fresh `InspectionEngine` per incoming inspection request.
- Let each request build one run-level `IFabricFileSystem` so repeated rule sets and parallel buckets reuse cached workspace item definitions.
- Prefer engine instance events (`InspectionEngine.MessageIssued`) per caller/session.
- Avoid sharing engine instances across users.

### 8.1 Request lifecycle in a web backend

```mermaid
sequenceDiagram
    participant U as User Browser
    participant API as Web API Endpoint
    participant DI as ServiceProvider
    participant ENG as InspectionEngine per request
    participant FS as Shared IFabricFileSystem per request
    participant H as InspectionContextHolder
    participant INS as Inspector and Operators

    U->>API: POST inspect
    API->>DI: Resolve HttpClient singleton
    API->>ENG: new InspectionEngine(HttpClient)
    API->>ENG: RunAndReturnResultsAsync(args, tokenProvider, ...)

    ENG->>H: PushScope(request context)
    H-->>ENG: request scope token
    ENG->>FS: Create once for the run
    ENG->>FS: Reuse cached definitions for every ruleset

    ENG->>INS: Execute rules
    INS->>H: Read and mutate Current in this async flow
    INS->>FS: Read workspace items and item definitions
    INS-->>ENG: Test results

    ENG->>H: Dispose scope
    ENG-->>API: TestRun
    API-->>U: HTTP response
```

### 8.2 Concurrent users and isolation boundaries

```mermaid
flowchart LR
  subgraph SharedProcess["Shared process resources"]
    HC["Singleton HttpClient"]
  end

  subgraph ReqA["Request A User A"]
    EA["Engine A"] --> SA["PushScope Context A"] --> RA["Run rules A"] --> DA["Dispose Scope A"]
  end

  subgraph ReqB["Request B User B"]
    EB["Engine B"] --> SB["PushScope Context B"] --> RB["Run rules B"] --> DB["Dispose Scope B"]
  end

  HC --> EA
  HC --> EB

  RA -. "No shared ambient state" .- RB

  classDef shared fill:#e8f5e9,stroke:#2e7d32,stroke-width:1px,color:#000
  classDef isolated fill:#e3f2fd,stroke:#1565c0,stroke-width:1px,color:#000
  class HC shared
  class EA,SA,RA,DA,EB,SB,RB,DB isolated
```

Implications:

- Shared network plumbing (`HttpClient`) is safe and desirable.
- The shared filesystem stays request-scoped, not process-scoped, so cached item definitions do not cross user boundaries.
- Ambient inspection state is isolated by `AsyncLocal` scope per async flow.
- Parallel execution inside one request remains isolated by per-task context cloning (section 4).
- Cross-user contamination is prevented as long as hosts do not reuse `InspectionEngine` instances between requests.