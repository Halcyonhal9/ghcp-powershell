---
mode: agent
description: Review and action all code review feedback on a PR. Read each comment, make the requested change (or explain why not), reply to the comment, and resolve the thread.
---

# Review and Action PR Feedback

Fetch all review comments on the current PR, action every one, reply, and resolve.

Arguments: ${input:pr_number:PR number (leave blank to auto-detect from current branch)}

## 1. Identify the PR

- If a PR number was provided above, use it.
- Otherwise, detect the PR for the current branch using the GitHub MCP tools.

## 2. Fetch review comments

Use the GitHub MCP tools to list all review comments on the PR.

For each comment, extract: `id`, `path`, `line`, `body`, `in_reply_to_id` (skip replies — only action root comments).

## 3. Action each comment

For every root comment:

1. **Read the file** at the referenced path and line to understand context.
2. **Decide**: make the change, or explain why not (e.g. false positive, out of scope, violates thin-wrapper principle).
3. **If making a change**: edit the file. If the change affects tests, update tests too.
4. **Track progress**: use a todo list to track each comment as a task.

## 4. Run tests

After all changes, run the test suite:
```
dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"
```
All tests must pass before proceeding.

## 5. Commit and push

- Stage only the changed files (not `git add -A`).
- Write a clear commit message summarising all review feedback addressed.
- Push to the current branch.

## 6. Reply and resolve

For each comment actioned, use the GitHub MCP tools to:

1. **Reply** to the comment thread explaining what was done.
2. **Resolve** the thread if supported.

## 7. Summary

Output a table summarising each comment and what was done:

| # | Comment | Action |
|---|---------|--------|
| 1 | Brief description | What was changed / why it was declined |
