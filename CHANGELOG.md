# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.4.0] - 2026-06-29

### Added
- Avalonia UI desktop project with macOS support, alongside macOS support for CLI builds.
- New `discover_rules` MCP tool with enhanced rule applicability filtering and a `rulesCatalogPath` parameter for `Inspect`/`DiscoverRules`.
- New example rules for union operations, variable library validation, workspace naming conventions, report structure checks, and themeable properties.
- Set intersect operator and corresponding rule.

### Changed
- Refactored `MainWindow` layout to use a `TabControl` for Inputs and Outputs sections.
- Refactored `Inspect` and `DiscoverRules` methods to include input validation.
- Refactored `ExampleRules` structure and updated documentation references.
- Revamped documentation structure and accuracy; clarified output formats, rules parameters, and authentication usage with OneLake URLs.

### Fixed
- Avalonia UI build issues and resolution of relative static file paths.
- `FileTextSearchCountRule` now works on non-local files, not just local ones.
- Cross-platform tests now resolve assets and paths correctly.
- Case sensitivity in documentation paths.
- Markdown links and operator name mismatch (renamed `intersect` to `intersection`).

### Removed
- Authentication requirement checks for rules in CLI argument parsing.
- README/Docs link check step from the CI workflow.
- Unnecessary timeout values in scanner API rules.

[3.4.0]: https://github.com/NatVanG/fab-inspector/compare/v3.3.0...v3.4.0
