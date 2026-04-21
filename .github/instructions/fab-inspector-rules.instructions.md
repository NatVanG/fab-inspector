---
description: Fab Inspector rule authoring, JSONLogic patterns, operators, part iterators, and patch construction.
---

When helping with Fab Inspector rule authoring, always read and follow:
.ai-assets/skills/fab-inspector-rules/SKILL.md

Focus on:
- valid rule object structure
- correct use of test arrays (logic, optional data mapping, expected result)
- part iterator usage (reserved Report parts and regex path matching)
- custom operator selection and syntax
- safe, deterministic rule behavior

Before creating or correcting Fab Inspector rules for Fabric items or Fabric REST APIs:
- Ensure the local Fabric MCP server is installed and available.
- Consult `docs_item_definitions` when determining valid definition part names, JSON structures, JSON Pointer paths, and property locations.
- Consult `docs_platform_api_spec` when authoring rules that call core Fabric platform REST APIs.
- Consult `docs_workload_api_spec` when authoring rules that call workload-specific Fabric REST APIs.
- Use these local Fabric MCP docs tools as needed to ground rule paths, parts, and API endpoints in the current official schemas/specs.

When writing or correcting JSON path/JSON Pointer examples for Fabric item definitions:
- Validate paths against the Fabric item-definition schema docs via `mcp_fabric_mcp_docs_item-definitions`.
- Prefer data-mapping pointers with a leading slash (JSON Pointer), then reference mapped values in logic via `var`.

Pointer and path conventions:
- Data mapping values use JSON Pointer syntax (example: `/properties/jobMode`).
- In logic, `var` uses dot notation for mapped objects (example: `job.properties.jobMode`).
- For array items, use numeric indexes in pointers (example: `/properties/activities/0/type`).
- Keep `part` aligned to definition part paths from schema docs.

Schema-backed examples (verified via Fabric item-definition docs):

CopyJob (`part: copyjob-content.json`):
```json
{
  "itemType": "CopyJob",
  "part": "copyjob-content.json",
  "test": [
    { "==": [{ "var": "job.properties.jobMode" }, "Batch"] },
    { "job": "/" },
    true
  ]
}
```

DataPipeline (`part: pipeline-content.json`):
```json
{
  "itemType": "DataPipeline",
  "part": "pipeline-content.json",
  "test": [
    { "==": [{ "var": "pipeline.properties.activities.0.type" }, "TridentNotebook"] },
    { "pipeline": "/" },
    true
  ]
}
```

VariableLibrary (`part: variables.json`):
```json
{
  "itemType": "VariableLibrary",
  "part": "variables.json",
  "test": [
    { "==": [{ "var": "vars.variables.0.type" }, "String"] },
    { "vars": "/" },
    true
  ]
}
```

VariableLibrary settings (`part: settings.json`):
```json
{
  "itemType": "VariableLibrary",
  "part": "settings.json",
  "test": [
    { ">": [{ "count": [{ "var": "cfg.valueSetsOrder" }] }, 0] },
    { "cfg": "/" },
    true
  ]
}
```

Lakehouse (`part: lakehouse.metadata.json`):
```json
{
  "itemType": "Lakehouse",
  "part": "lakehouse.metadata.json",
  "test": [
    { "==": [{ "var": "meta.defaultSchema" }, "dbo"] },
    { "meta": "/" },
    true
  ]
}
```

Common pointer examples by part:
- `copyjob-content.json`: `/properties/jobMode`, `/activities/0/properties/destination/writeBehavior`
- `pipeline-content.json`: `/properties/activities/0/type`, `/properties/activities/0/typeProperties/notebookId`
- `variables.json`: `/variables/0/name`, `/variables/0/value`
- `settings.json`: `/valueSetsOrder/0`
- `lakehouse.metadata.json`: `/defaultSchema`
- `shortcuts.metadata.json`: `/0/path`, `/0/target/type`
- `data-access-roles.json`: `/0/name`, `/0/decisionRules/0/effect`
- `alm.settings.json`: `/version`, `/objectTypes/0/name`
