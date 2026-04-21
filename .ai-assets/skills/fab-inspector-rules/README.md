# fab-inspector-rules

Rule authoring skill for the Fab Inspector testing tool.

## What's in this skill

The [SKILL.md](SKILL.md) file provides AI agents with complete guidance on:

- **Rule file structure** — the `{ "rules": [...] }` envelope and all rule properties (`id`, `name`, `description`, `logType`, `itemType`, `disabled`, `part`, `test`, `patch`)
- **Part iterator** — reserved Report part names (`Pages`, `Visuals`, `Bookmarks`, etc.) and regex-based file matching with colon path separators
- **Test definitions** — the 2/3-element `[logic, data-mapping, expected]` array pattern
- **Built-in JSONLogic operators** — comparison, logic, arithmetic, array, string, data
- **36+ custom operators** — set operations (`diff`, `intersection`, `union`, `symdiff`, `equalsets`), string operations (`strcontains`, `strsplit`, `strjoin`, `regexextract`), navigation (`part`, `partinfo`, `query`, `path`, `drillvar`), file system (`filesize`, `filetextsearchcount`, `fromyamlfile`), data (`count`, `torecord`, `distinct`, `keys`, `values`, `typeof`, `hasprop`, `isnullorempty`, `coalesce`, `slice`, `tostring`), variable binding (`let`), date/time (`now`, `datediff`), layout (`rectoverlap`), and REST API (`daxquery`, `apiget`, `dfsget`, `scannerapi`)
- **Patches** — RFC 6902 JSON Patch for auto-fixing failing items
- **Item types** — Report, CopyJob, Lakehouse, SemanticModel, DataPipeline, and more
- **Rule creation walkthrough** — 10 progressive examples from trivial to complex
- **Common patterns and idioms** — quick reference for frequent rule-writing techniques

## When to load this skill

Load this skill when a user needs to:

- Write new inspection rules
- Edit or debug existing rules
- Understand what operators are available
- Learn the rule file format
- Create cross-item or API-driven rules
- Set up patches for auto-remediation

## Related

- **[fab-inspector-cli](../fab-inspector-cli/)** — CLI invocation skill (parameters, authentication, output formats)
