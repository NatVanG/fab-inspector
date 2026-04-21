# Skills

Skills are task-specific instruction sets that guide AI agents when helping users with Fab Inspector operations.

## Available Skills

| Skill | Description |
|-------|-------------|
| `fab-inspector-cli` | CLI invocation — parameters, authentication methods, output formats, usage scenarios, validation rules |
| `fab-inspector-rules` | Rule authoring — rule structure, operators, test definitions, patches, item types, creation walkthrough |

## Usage

Load skills based on the user's task:

- **Always load `fab-inspector-cli`** when the user needs to run the inspector, set up pipelines, or configure authentication.
- **Always load `fab-inspector-rules`** when the user needs to write, edit, or understand inspection rules.
- Load **both** when building end-to-end workflows.

## Skill Structure

Each skill folder contains:

```
<skill-name>/
├── SKILL.md           # Main skill definition (load this)
├── README.md          # Human-readable overview
├── references/        # (future) Detailed reference docs and examples
└── scripts/           # (future) Automation scripts
```
