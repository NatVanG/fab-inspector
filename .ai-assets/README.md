# AI Assets for Fab Inspector

This folder contains AI-related resources for agents working with the Fab Inspector project — a deterministic, rules-based testing tool for Microsoft Fabric.

## Folder Structure

| Folder | Purpose |
|--------|---------|
| `skills/` | Task-specific skill definitions for AI agents |
| `context/` | *(planned)* Background context and contributor instructions for different AI tools |
| `modes/` | *(planned)* Custom agents (chat modes) for VS Code Copilot |
| `prompts/` | *(planned)* Reusable prompt templates |

## Skills

| Skill | Path | Description |
|-------|------|-------------|
| fab-inspector-cli | `skills/fab-inspector-cli/SKILL.md` | CLI invocation, parameters, authentication, output formats, usage scenarios |
| fab-inspector-rules | `skills/fab-inspector-rules/SKILL.md` | Rule authoring, operators, test definitions, patches, item types |

### Usage

Load skills based on the user's task:

- Load **`fab-inspector-cli`** when running inspections, setting up CI/CD pipelines, configuring authentication, or troubleshooting CLI invocation.
- Load **`fab-inspector-rules`** when writing, editing, or debugging inspection rules, understanding operators, or learning the rule file format.
- Load **both** when creating an end-to-end workflow (e.g., writing rules and then running them).

## Future Extensibility

| Area | Purpose | Notes |
|------|---------|-------|
| `context/` | Copilot instructions, CLAUDE.md, cursorrules | Currently served by `.github/copilot-instructions.md` |
| `modes/` | Custom VS Code Copilot agents | e.g., a "Rule Author" agent mode |
| `prompts/` | Prompt templates | e.g., "create-rule", "setup-cicd" templates |
| `skills/*/scripts/` | Automation scripts per skill | e.g., rule validation helpers |
| `skills/*/references/` | Detailed reference docs | e.g., per-operator examples, full command reference |
