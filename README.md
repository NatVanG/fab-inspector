[![CodeQL](https://github.com/NatVanG/fab-inspector/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/NatVanG/fab-inspector/actions/workflows/github-code-scanning/codeql)
[![PBIRInspector Tests](https://github.com/NatVanG/fab-inspector/actions/workflows/tests.yml/badge.svg)](https://github.com/NatVanG/fab-inspector/actions/workflows/tests.yml)
[![Build and Publish Docker Image](https://github.com/NatVanG/fab-inspector/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/NatVanG/fab-inspector/actions/workflows/docker-publish.yml)

# Fab Inspector

## Deterministic rules-based testing for Microsoft Fabric in the age of AI

Meet Ric, the Fab Inspector.

<div style="display: flex; gap: 20px; align-items: center;">
  <img src="Docs/Images/Ric_480x480_speech.png" alt="Hi! I'm Ric, the Fab Inspector." height="300"/>
  <img src="Docs/Images/FabInsp_500x500.png" alt="Fab Inspector logo" height="240"/>
</div>

## Note

This is a community project and is not supported by Microsoft.

Power BI report testing supports enhanced metadata format (PBIR). PBIX files are not currently supported; use PBIP or `{my-report}.Report` folders.

## What It Does

Fab Inspector validates Fabric artifacts with declarative JSON rules. You can run the same rules:

- Locally against checked-out item definitions
- In CI/CD pipelines (ADO or GitHub log formats)
- Against published Fabric workspace items with authentication

Beyond artifact structure, Fab Inspector can also inspect:

- **Tenant and capacity configurations** — validate tenant posture against your governance policies through API operators
- **Data-based checks** — use DAX query and T-SQL query operators as rule conditions to enforce data quality, semantic model correctness, and lakehouse health

Outputs can be written as Console, JSON, HTML, ADO, or GitHub formats.

## Quick Start

### Desktop GUI (Avalonia)

![AvaloniaUI](Docs/Images/FabInspector.AvaloniaUI.png)

The cross-platform `FabInspector.AvaloniaUI` desktop application lets you run inspections without the CLI. Point it at a local Fabric item folder or a Fabric workspace, choose a rules file and output format, and click **Run**. Results are written to a local folder or directly to OneLake, and can auto-open in the browser as HTML. See the [GUI Reference](Docs/gui-reference.md) for a full walkthrough.

### CLI

Local mode:

```bash
fab-inspector -fabricitem "C:\Files\Sales.Report" -rules ".\Files\Base-rules.json" -formats "Console,JSON"
```

Workspace mode (with interactive auth):

```bash
fab-inspector -fabricworkspace "<workspace-guid>" -rules ".\Files\Base-rules.json" -authmethod interactive -formats "Console,JSON"
```

Show CLI options:

```bash
fab-inspector -help
```
## Releases

Release binaries for the CLI and GUI are published at: https://github.com/NatVanG/fab-inspector/releases

## Documentation

| Document | Description |
|---|---|
| [Intro](Docs/intro.md) | Conceptual overview of Fab Inspector and why declarative JSON rules matter |
| [Usage Scenarios](Docs/usage-scenarios.md) | Common local, CI/CD, workspace, and hybrid validation patterns |
| [GUI Reference](Docs/gui-reference.md) | Cross-platform Avalonia desktop application walkthrough |
| [CLI Reference](Docs/cli-reference.md) | Full CLI parameters, auth options, and command examples |
| [Rules Guide](Docs/rules-guide.md) | Rule schema, test logic, and patching guidance |
| [Operators Overview](Docs/operators-overview.md) | When to use Ric vs FabInspector operators |
| [Examples Index](Docs/examples-index.md) | Catalog of rule files in ExampleRules by use case |
| [Ric Operators](Docs/Ric-Operators.md) | Local/query/transformation/file-system operators |
| [FabInspector Operators](Docs/FabInspector-Operators.md) | API, DAX, SQL, and DFS operators |
| [Architecture](FabInspector.Core/Architecture.md) | Inspection engine and DI/concurrency internals |

## Base Rules

Fab Inspector ships with base rules at [Rules/Base-rules.json](Rules/Base-rules.json), focused on report visual quality checks.

To customize:

1. Copy the base rules file.
2. Edit parameter values or set `"disabled": true` on unwanted rules.
3. Point `-rules` to your customized file.

## Agentic rule creation

The creation of rules for Fab Inspector can feel unfamiliar, luckily an AI agent is available via this repo: https://github.com/NatVanG/fab-inspector-test-harness/blob/main/README.md. This capability is being actively developed and improved, always review AI generated output for accuracy. 

## CI/CD

Fab Inspector supports CI/CD pipeline integration:

- `-formats ADO` for Azure DevOps task commands
- `-formats GitHub` for GitHub Actions annotations/logging

Tutorial and sample links are documented in [CLI Reference](Docs/cli-reference.md).

## Wiki

For deep-dive guidance and advanced operator/rule examples, see the [Fab Inspector wiki](https://github.com/NatVanG/fab-inspector/wiki).

## Thanks

Thanks to [Michael Kovalsky](https://github.com/m-kovalsky), [Rui Romano](https://github.com/ruiromano), [Luke Young](https://www.linkedin.com/in/luke-young-2301/), and [David Mitchell](https://www.linkedin.com/in/davidmitchell85) for support and feedback.

## Report an Issue

Please report issues at https://github.com/NatVanG/fab-inspector/issues.
