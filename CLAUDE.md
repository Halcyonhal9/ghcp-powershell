# CLAUDE.md — Project Guidelines for CopilotCmdlets

## Core Principles

- **Thin SDK wrapper.** This module is a pass-through to the `GitHub.Copilot.SDK` NuGet package. Every cmdlet delegates directly to SDK methods. Do not invent business logic.
- **No custom features.** If a capability does not exist in the SDK, we do not implement it. File an SDK feature request instead of writing workaround code.
- **Always test.** Every cmdlet, helper, and code path must have corresponding test coverage. No code is merged without tests — unit tests at minimum, end-to-end where applicable.

## Code Style

- **camelCase** for local variables and private fields.
- **PascalCase** for public members, types, and method names (standard C#).
- Follow existing patterns in the codebase — look at neighboring code before adding new files.

## Architecture

- Five C# source files in `src/`, one test project in `tests/`. Keep it flat.
- `ModuleState` is the only singleton. Cmdlets are stateless beyond what `ModuleState` holds.
- Cmdlets accept explicit `-Client` / `-Session` parameters; fall back to `ModuleState` defaults.
- No custom abstractions unless required for testability (e.g., thin interface wrappers around sealed SDK types).

## Testing

- **Framework:** xUnit + NSubstitute.
- **Unit tests** (`tests/Unit/`): Mock SDK types, no network or CLI required. Tagged `[Trait("Category", "Unit")]`.
- **End-to-end tests** (`tests/EndToEnd/`): Real SDK against a running Copilot CLI. Tagged `[Trait("Category", "EndToEnd")]`. Require `GITHUB_TOKEN` env var.
- Run unit tests: `dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"`
- Run e2e tests: `dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=EndToEnd"`
- Run all tests: `dotnet test tests/CopilotCmdlets.Tests.csproj`

## Build

```bash
dotnet publish src/CopilotCmdlets.csproj -c Release -o out
```

Or use the convenience script:

```bash
pwsh build.ps1
```

## Development Environment

- Open the workspace: `CopilotCmdlets.code-workspace`
- Debug configurations are in `.vscode/launch.json` (attach to pwsh, run tests)
- Target: .NET 9 / PowerShell 7.6+
