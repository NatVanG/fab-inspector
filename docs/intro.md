# Intro

Fab Inspector is a deterministic validator for Microsoft Fabric deployments. It lets teams codify quality expectations as JSON rules, run those rules against local files or published workspace items, and produce repeatable pass/fail results that are easy to automate.

In practice, Fab Inspector acts like a unit test runner for Fabric artifacts. Instead of asserting behavior in application code, you assert metadata contracts: naming conventions, report design guardrails, CopyJob and DataPipeline settings and even API-derived checks via built-in operators. The result is policy-as-code for Fabric solutions, with outputs that fit both developer feedback loops and enterprise pipelines.

Rules are expressed as declarative JSON rather than imperative scripts or custom code. This has practical advantages over alternatives such as PowerShell scripts, Python notebooks, or hard-coded validation logic:

- **No compilation or runtime dependencies** - rules are plain JSON files that any text editor, version control system, or LLM can read, generate, and diff without a build step.
- **Separation of concerns** - validation intent lives outside application code, so rules evolve independently and can be owned by different personas (architects, governance teams, or AI agents).
- **Composable and parameterised** - rules support variables, conditional logic via [JSONLogic](https://jsonlogic.com/), and operator extensions (REST API calls, DAX queries, OneLake reads) without writing code.
- **Portable** - the same JSON rules file runs locally on a developer's machine, in Azure DevOps pipelines, and in GitHub Actions with no modification.
- **AI-friendly** - the declarative structure is easy for language models to generate, review, and explain, making rules a natural artefact in agentic workflows.

Fab Inspector is designed with agentic development workflows in mind. AI agents can generate or refactor Fabric items quickly, but speed and non-deterministic outputs increase the need for reliable validation gates. Fab Inspector provides such gates by:

- validating agent-produced artifacts before merge or deployment
- enforcing team standards consistently across item types
- surfacing actionable failures in Console, HTML, JSON, Azure DevOps, or GitHub formats
- optionally logging validation results as JSON to Fabric OneLake for post-hoc reporting
- enabling fast local checks and scalable CI/CD quality controls with the same rule model

Whether you are authoring rules locally, running checks in pull requests, or validating an entire Fabric workspace, Fab Inspector helps teams move faster with confidence by making quality requirements explicit, testable, and automatable.
