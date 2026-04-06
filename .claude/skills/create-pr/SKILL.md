---
name: create-pr
description: Create a GitHub pull request for the current branch with a structured summary and test plan.
allowed-tools: Bash, Read, Grep, Glob
---

# Create Pull Request

Create a pull request for the current branch. Follow these steps exactly.

## 1. Gather context

Run these in parallel:
- `git status` (never use `-uall`)
- `git diff` to see staged + unstaged changes
- `git log --oneline origin/main...HEAD` to see all commits on this branch
- `git diff origin/main...HEAD --stat` for a file-level summary
- Check if the branch tracks a remote and is up to date

## 2. Analyse changes

Review ALL commits on the branch (not just the latest). Understand:
- What changed and why
- Which files were modified
- Whether tests were added/updated

## 3. Draft the PR

- **Title**: Under 70 characters. Summarise the change, not the commits.
- **Body**: Use the template below.

## 4. Push and create

- Push with `git push -u origin <branch-name>` if needed.
- Use the MCP GitHub tools (`mcp__github__*`) to check for an existing PR on this branch and create or update accordingly.
  - If an existing PR is **open**, update its title and body instead of creating a new one.
  - If no open PR exists, create a new one with base `main`.
- Extract the PR URL from the response.

## 5. Output

- **Never wrap PR URLs in markdown bold (`**`) or other markdown formatting.** Output the bare URL so it remains a clickable link.
- Return the bare PR URL as the final output.

## PR body template

```
## Summary
<1-3 bullet points describing what changed and why>

## Test plan
- [ ] `dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"` passes
- [ ] `dotnet publish src/CopilotCmdlets.csproj -c Release -o out` succeeds
- [ ] <Additional verification steps>

<session-link>
```

## Arguments

$ARGUMENTS
