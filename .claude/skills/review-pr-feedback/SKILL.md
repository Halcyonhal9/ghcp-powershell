---
name: review-pr-feedback
description: Review and action all code review feedback on a PR. Read each comment, make the requested change (or explain why not), reply to the comment, and resolve the thread.
allowed-tools: Bash, Read, Edit, Write, Grep, Glob, Agent
---

# Review and Action PR Feedback

Fetch all review comments on the current PR, action every one, reply, and resolve.

## 1. Identify the PR

- If an argument is provided (`$ARGUMENTS`), use that as the PR number.
- Otherwise, detect the PR for the current branch:
  ```
  gh api repos/{owner}/{repo}/pulls --jq '.[] | select(.head.ref == "<branch>") | .number'
  ```
- If the remote is not a standard GitHub host, use `gh api` with the repo path from `git remote -v`.

## 2. Fetch review comments

```
gh api repos/{owner}/{repo}/pulls/{number}/comments
```

For each comment, extract: `id`, `path`, `line`, `body`, `in_reply_to_id` (skip replies — only action root comments).

## 3. Action each comment

For every root comment:

1. **Read the file** at the referenced path and line to understand context.
2. **Decide**: make the change, or explain why not (e.g. false positive, out of scope).
3. **If making a change**: edit the file. If the change affects tests, update tests too.
4. **Track progress**: use the TodoWrite tool to track each comment as a task.

## 4. Run tests

After all changes, run the test suite:
```
pytest -m "not e2e and not e2e_all_providers and not e2e_all_models" -x -q
```
All tests must pass before proceeding.

## 5. Commit and push

- Stage only the changed files (not `git add -A`).
- Write a clear commit message summarising all review feedback addressed.
- Push to the current branch.

## 6. Reply and resolve

For each comment actioned:

1. **Reply** to the comment thread explaining what was done:
   ```
   gh api repos/{owner}/{repo}/pulls/{number}/comments \
     -f body="<response>" -F in_reply_to=<comment-id>
   ```
2. **Resolve** the thread via GraphQL:
   - First fetch thread IDs:
     ```
     gh api graphql -f query='{ repository(owner: "...", name: "...") {
       pullRequest(number: N) { reviewThreads(first: 50) { nodes { id isResolved
         comments(first: 1) { nodes { databaseId } } } } } } }'
     ```
   - Then resolve each unresolved thread:
     ```
     gh api graphql -f query='mutation { resolveReviewThread(input: {threadId: "..."})
       { thread { isResolved } } }'
     ```

## 7. Summary

Output a table summarising each comment and what was done:

| # | Comment | Action |
|---|---------|--------|
| 1 | Brief description | What was changed / why it was declined |

## Arguments

$ARGUMENTS
