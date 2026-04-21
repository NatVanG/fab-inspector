## Fab Inspector Skill Wiring

- Skill-backed instructions are defined under `.github/instructions/`.
- Use `fab-inspector-cli.instructions.md` for CLI invocation, authentication, output formats, and CI/CD guidance.
- Use `fab-inspector-rules.instructions.md` for rule authoring, operator usage, and patch construction.
- Source skills are in `.ai-assets/skills/fab-inspector-cli/SKILL.md` and `.ai-assets/skills/fab-inspector-rules/SKILL.md`.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
