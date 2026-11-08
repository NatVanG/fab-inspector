# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [4.0.0] - 2026-10-27

### Breaking
- The CLI now requires the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). .NET 8 reaches end of support on November 10, 2026.

### Changed
- Upgraded all projects from .NET 8 to .NET 10 (LTS), ahead of .NET 8 end of support on November 10, 2026. The framework-dependent CLI builds now require the .NET 10 runtime; the WinForms and Avalonia UI builds remain self-contained.
- Docker image now uses the `mcr.microsoft.com/dotnet/runtime:10.0` and `sdk:10.0` base images, and CI/CD workflows build with the .NET 10 SDK. Release archives are renamed from `portable-net8.0` to `portable-net10.0`.
- Added `global.json` pinning the .NET 10 SDK.
- Certificate credentials with a password now load via `X509CertificateLoader.LoadPkcs12FromFile` (replacing the obsolete `X509Certificate2` constructor, SYSLIB0057).

### Security
- Bumped `Microsoft.Data.SqlClient` to 6.1.7 and pinned `System.Security.Cryptography.Pkcs` 10.0.14 to override a vulnerable transitive dependency (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7).

[4.0.0]: https://github.com/NatVanG/fab-inspector/compare/v3.5.0...v4.0.0

## [3.5.0] - 2026-07-22

### Added
- Rule tags filtering: new `-tags` CLI option and `tags` MCP tool parameter to run only rules containing any matching tag (case-insensitive); an empty value runs all applicable rules. Matched tags are now surfaced in JSON and HTML output.
- `fabricItemTypes` parameter for `discover_rules`, allowing rule discovery to filter by one or more Fabric item types without resolving a concrete item.

### Changed
- `DiscoverRules` now exposes an async `DiscoverRulesAsync` method and propagates the active tags filter through the inspection engine and output context.
- Updated CLI, operators, examples, intro, and usage-scenarios documentation, including a corrected CI/CD pipeline reference link.

[3.5.0]: https://github.com/NatVanG/fab-inspector/compare/v3.4.0...v3.5.0

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
