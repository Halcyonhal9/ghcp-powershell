---
name: review-pr
description: Review a GitHub pull request — analyze the diff, post inline comments on specific code sections, and provide a summary.
argument-hint: "[pr-number]"
---

# Code Review Skill

Review pull request #$ARGUMENTS thoroughly and post a review with inline comments.

## Steps

1. **Fetch PR metadata and diff**:
   - Use MCP GitHub tools (`mcp__github__*`) to get the PR title, description, base branch, and diff.
   - If the diff is large, save it to a temp file and read it.

2. **Read surrounding context**: For each changed file, read the full file (not just the diff) to understand the broader context — unchanged code, method signatures, class structure. This is critical for catching issues the diff alone won't reveal.

3. **SDK-wrapper validation (MANDATORY)**: Before analyzing anything else, check every changed file against the project's core principles from CLAUDE.md and docs/initial-design.md. This is a blocking review concern — flag violations as top-priority issues.
   - Verify that cmdlets are **thin wrappers** around the `GitHub.Copilot.SDK` NuGet package — no custom business logic.
   - Flag any code that **reimplements SDK functionality** — if the SDK already has a method, type, or capability for it, the PR must use it.
   - Flag any code that invents features not present in the SDK. The project does not implement workaround code; file an SDK feature request instead.
   - Verify that `ModuleState` remains the only singleton and cmdlets are stateless beyond what `ModuleState` holds.
   - Verify cmdlets accept explicit `-Client` / `-Session` parameters with fallback to `ModuleState` defaults.

4. **Analyze the changes** looking for:
   - **Correctness**: Logic errors, off-by-one, race conditions, null handling.
   - **Security**: Injection, path traversal, auth bypass, secrets in code, OWASP top 10.
   - **Resource leaks**: Undisposed objects, missing cleanup in error paths (IDisposable / IAsyncDisposable).
   - **Error handling**: Swallowed exceptions, missing error paths, unhelpful error messages.
   - **API design**: Breaking changes, inconsistent naming, missing validation at boundaries.
   - **Consistency**: Does the change follow existing codebase conventions (from CLAUDE.md)? camelCase locals, PascalCase publics, flat architecture.
   - **Test coverage**: Are new code paths tested? Unit tests with xUnit + NSubstitute? Correct `[Trait("Category", "Unit")]` or `[Trait("Category", "EndToEnd")]` tags?
   - **Documentation**: Do XML doc comments match the implementation?

5. **Post the review** using MCP GitHub tools (`mcp__github__*`) to submit a pull request review with:
   - A summary body (see format below)
   - Inline comments with file path, line number, and body
   - Event type: `COMMENT`

6. **Report the result**: Show the review URL and a concise summary of findings to the user.

## Review body format

```
## Code Review Summary

[1-2 sentence overall assessment of the PR quality and design direction.]

### SDK compliance

[Report on whether the PR properly wraps the GitHub.Copilot.SDK. Flag any reimplemented SDK features, custom business logic, or features not present in the SDK. If fully compliant, state so explicitly.]

### Issues to address

1. **[Short title]** (`File.cs`): [Description of the issue and suggested fix.]
2. ...

### Minor / Nits

- [Optional section for style, naming, or non-blocking suggestions.]

### Positive notes

- [Call out things done well — good test coverage, clean design, etc.]
```

## Inline comment format

Each inline comment should:
- Start with a **bold label** indicating severity: e.g. `**SDK Violation:**`, `**Bug:**`, `**Security:**`, `**Nit:**`, `**Question:**`
- Explain the issue clearly and concisely.
- Include a suggested fix (code snippet) when possible.

## Guidelines

- Be constructive, not pedantic. Focus on issues that matter.
- Don't flag things that are clearly intentional design choices without strong reason.
- Respect the codebase conventions defined in CLAUDE.md (camelCase locals, PascalCase publics, xUnit + NSubstitute, .NET 10).
- If the PR has tests, verify they actually test the new behavior — not just that tests exist.
- Limit inline comments to the most impactful findings (aim for 3-8 comments, not 20).
