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
- **If a PR was already created earlier in this conversation**, check its status before creating another:
  `gh api "repos/{owner}/{repo}/pulls?head={owner}:{branch}&state=all" --jq '.[0] | {number, state, html_url}'`
  - **Important**: Use query string params (`?key=value`) for this GET request — `-f` flags trigger a POST.
  - If the existing PR is **open**, update its title and body with `--method PATCH` instead of creating a new one.
  - If the existing PR is **closed/merged**, create a new PR (the old one cannot be reused).
- **If this is the first PR in the conversation**, skip the check and create directly.
- Create the PR using `gh api repos/{owner}/{repo}/pulls --method POST`.
- Pass title, head, base, and body as `-f` flags. Use `-f base=main`.
- Extract the URL from the response with `--jq '.html_url'`.

## 5. Output

- **Never wrap PR URLs in markdown bold (`**`) or other markdown formatting.** Output the bare URL so it remains a clickable link.
- Return the bare PR URL as the final output.

## PR body template

```
## Summary
<1-3 bullet points describing what changed and why>

## Test plan
- [ ] <Checklist of verification steps>

<session-link>
```

## Arguments

$ARGUMENTS
