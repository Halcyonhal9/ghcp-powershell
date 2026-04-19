# Copilot Instructions

This repository uses a single shared set of coding guidelines for all AI coding agents. The canonical source is [CLAUDE.md](../CLAUDE.md) at the repository root.

**Read and follow [CLAUDE.md](../CLAUDE.md) in full.** It defines:

- Core principles (thin SDK wrapper, no custom features, always test)
- Code style (camelCase locals, PascalCase publics)
- Architecture (flat `src/`, `ModuleState` as the only singleton, explicit `-Client` / `-Session` parameters)
- Testing (xUnit + NSubstitute, `[Trait("Category", "Unit")]` vs `[Trait("Category", "EndToEnd")]`)
- Build and development environment

If guidance in this file ever conflicts with [CLAUDE.md](../CLAUDE.md), `CLAUDE.md` wins — update it there rather than duplicating rules here.
