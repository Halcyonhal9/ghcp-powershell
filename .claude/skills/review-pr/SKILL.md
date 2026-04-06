---
name: review-pr
description: Review a GitHub pull request — analyze the diff, post inline comments on specific code sections, and provide a summary.
argument-hint: "[pr-number]"
---

# Code Review Skill

Review pull request #$ARGUMENTS thoroughly and post a review with inline comments.

## Steps

1. **Fetch PR metadata and diff**:
   - Run `GH_REPO="Halcyonhal9/lumi2" gh pr view $ARGUMENTS` to get the PR title, description, and base branch.
   - Run `GH_REPO="Halcyonhal9/lumi2" gh pr diff $ARGUMENTS` to get the full diff.
   - If the diff is large, save it to a temp file and read it.

2. **Read surrounding context**: For each changed file, read the full file (not just the diff) to understand the broader context — unchanged code, function signatures, module structure. This is critical for catching issues the diff alone won't reveal.

3. **SDK-first validation (MANDATORY)**: Before analyzing anything else, check every changed file against the Copilot SDK. This is a blocking review concern — flag violations as top-priority issues.
   - Read the installed SDK source at `/usr/local/lib/python3.11/dist-packages/copilot/` (especially `types.py`, `session.py`, `client.py`, `tools.py`) to understand what the SDK already provides.
   - Consult `docs/sdk/audit-copilot-sdk.md` and `docs/sdk/sdk-knowledge-base.md` for known SDK capabilities and project conventions.
   - Flag any code that **reimplements SDK functionality** — if the SDK already has a method, type, hook, or capability for it, the PR must use it. Custom alternatives require a `# DIVERGES-FROM-SDK: <reason>` comment and explicit justification.
   - Flag any code that **builds or generates prompts based on logic** (string concatenation, template rendering, conditional prompt assembly, f-string prompt construction). All prompt manipulation must go through the SDK's built-in capabilities (system prompts, attachments, hooks, tool schemas, etc.). Constructing prompts in application code is not acceptable.
   - Flag any **direct HTTP calls to model APIs** that bypass the SDK. All LLM calls — including lightweight/utility calls like chat naming, memory extraction, summarization — must go through the Copilot SDK.
   - If the PR introduces a new integration pattern, verify it doesn't duplicate something the SDK already supports (e.g. attachments, streaming, session resume, model capabilities, tool definitions).

4. **Analyze the changes** looking for:
   - **Correctness**: Logic errors, off-by-one, race conditions, null/undefined handling.
   - **Security**: Injection, path traversal, auth bypass, secrets in code, OWASP top 10.
   - **Resource leaks**: Unclosed handles, unjoined threads, missing cleanup in error paths.
   - **Error handling**: Swallowed exceptions, missing error paths, unhelpful error messages.
   - **API design**: Breaking changes, inconsistent naming, missing validation at boundaries.
   - **Consistency**: Does the change follow existing codebase conventions (from CLAUDE.md)?
   - **Test coverage**: Are new code paths tested? Are edge cases covered?
   - **Documentation**: Do docstrings match the implementation?

5. **Post the review** using the GitHub API:
   - Get the PR head commit SHA: `GH_REPO="Halcyonhal9/lumi2" gh api repos/Halcyonhal9/lumi2/pulls/$ARGUMENTS --jq '.head.sha'`
   - Build a JSON payload with:
     - `commit_id`: the head SHA
     - `event`: `"COMMENT"`
     - `body`: a summary of all findings (see format below)
     - `comments`: array of inline comments with `path`, `line` (in the new file), `side: "RIGHT"`, and `body`
   - Submit via: `GH_REPO="Halcyonhal9/lumi2" gh api repos/Halcyonhal9/lumi2/pulls/$ARGUMENTS/reviews --method POST --input <payload-file>`
   - If inline comments fail with "line could not be resolved", adjust line numbers to match the actual file lines on the RIGHT side of the diff.

6. **Report the result**: Show the review URL and a concise summary of findings to the user.

## Review body format

```
## Code Review Summary

[1-2 sentence overall assessment of the PR quality and design direction.]

### SDK compliance

[Report on whether the PR properly uses the Copilot SDK. Flag any reimplemented SDK features, hand-built prompts, or direct LLM API calls. If fully compliant, state so explicitly.]

### Issues to address

1. **[Short title]** (`file.py`): [Description of the issue and suggested fix.]
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
- Respect the codebase conventions defined in CLAUDE.md (camelCase, pytest, cross-platform paths, etc.).
- If the PR has tests, verify they actually test the new behavior — not just that tests exist.
- Limit inline comments to the most impactful findings (aim for 3-8 comments, not 20).
