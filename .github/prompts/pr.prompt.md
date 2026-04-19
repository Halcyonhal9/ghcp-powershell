---
mode: agent
description: End-to-end PR workflow — create PR, simplify code, independent code review, and address feedback.
---

# PR Workflow

End-to-end process for creating a pull request, simplifying code, reviewing it independently, and addressing feedback. This workflow chains four prompts in sequence:

1. **create-pr** — create the PR (main context)
2. **code-simplifier** — simplify changed code for clarity and quality (isolated subagent)
3. **review-pr** — independent code review (isolated subagent)
4. **review-pr-feedback** — address review comments (main context)

Arguments: ${input:pr_number:Optional existing PR number — if provided, skip Step 1 and start from Step 2}

## Process

### Step 0: Branch guard

Before anything else, check whether the current branch needs a new branch for the PR.

1. Run `git branch --show-current` to get the current branch name.
2. Run `git worktree list` to check if the current working directory is a worktree.
3. **Create a new branch if** the current branch is `main` (or `master`) **or** the working directory is a git worktree (i.e. `git worktree list` shows the current directory as a linked worktree, not the main working tree).
   - Ask the user for a branch name, or derive one from the staged changes / recent commits (e.g. `feat/short-description`).
   - Run `git checkout -b <new-branch>` to create and switch to the new branch.
   - Record the **original branch name** — you will switch back to it in Step 6.
4. If already on a feature branch (not main, not a worktree), record the current branch as the original branch and continue normally.

### Step 1: Create the pull request

Invoke the `create-pr` prompt in the current conversation so it can leverage the full history of what was built and why. Capture the PR number from the output — you will need it for the next steps.

### Step 2: Simplify changed code (isolated subagent)

**This step MUST run in a subagent so that simplification changes are committed as a separate, clean pass.**

Use the `runSubagent` tool (or the `Explore` agent if that is all that is available — otherwise launch a general-purpose subagent) to review and simplify the code changed on this branch. The subagent:
- Reads `.claude/plugins/pr-review-toolkit/agents/code-simplifier.md` at runtime for its instructions
- Focuses only on files changed on the branch relative to `origin/main`
- Makes direct edits, runs tests, commits, and pushes

Subagent prompt:

```
You are simplifying code that was changed on this branch before it goes through code review.

IMPORTANT: Read the file .claude/plugins/pr-review-toolkit/agents/code-simplifier.md for the full
instructions and follow them exactly.

Read CLAUDE.md for project conventions.

After making changes, commit them with a message like "Simplify code for clarity and consistency"
and push to the current branch.

If there is nothing to simplify, just say so — do not force unnecessary changes.
```

Wait for the subagent to complete and capture its summary and any token/duration metrics it reports.

### Step 3: Independent code review (isolated subagent)

**This step MUST run in a subagent to ensure review independence.**

Spawn a subagent that performs the review. The subagent:
- Has NO access to the conversation that created the code
- Only sees the PR diff, the repo, and the review-pr prompt instructions
- Reads `.github/prompts/review-pr.prompt.md` at runtime for its review instructions

Subagent prompt:

```
You are performing an independent code review of PR #<PR_NUMBER> on this repository.

IMPORTANT: Read the file .github/prompts/review-pr.prompt.md for the full review
instructions and follow them exactly. That file contains the review methodology,
severity labels, output format, and GitHub API commands to post the review.

Read CLAUDE.md for project conventions.

The PR number is: <PR_NUMBER>

Execute all steps in the review-pr prompt now.
```

**Why a subagent?** The code review must be independent of the creation process. A subagent starts with a blank conversation — it cannot see the decisions, trade-offs, or rationale from the code creation step. This lets it catch issues that someone embedded in the creation context might miss.

**Why read the prompt at runtime?** So that any updates to the review-pr prompt immediately take effect in the workflow without modifying this orchestrator.

Wait for the subagent to complete and capture its review summary and any metrics.

### Step 4: Address review feedback (main context)

Invoke the `review-pr-feedback` prompt in the main conversation context, passing the PR number. Running it in the main context ensures it has:
- The full history of WHY code was written the way it was
- Context to distinguish valid review feedback from misunderstandings
- Ability to make informed decisions about which feedback to accept vs. push back on

### Step 5: Summary

After all four steps complete, present a summary:

| Phase | Status | Details |
|-------|--------|---------|
| PR created | Done | PR #N — <url> |
| Code simplified | Done | N files reviewed, N changes made |
| Code review | Done | N issues found, N inline comments posted |
| Feedback addressed | Done | N comments resolved, N declined with explanation |

Then print any token / duration metrics captured from the subagents in Steps 2 and 3. The main-context steps (create-pr and review-pr-feedback) run inside the orchestrator's conversation, so their tokens are part of the overall conversation usage and cannot be isolated.

### Step 6: Build verification

After the summary, confirm the module builds and tests pass:

```bash
dotnet publish src/CopilotCmdlets.csproj -c Release -o out
dotnet test tests/CopilotCmdlets.Tests.csproj --filter "Category=Unit"
```

Report any failures. If everything passes, note it in the summary.

**As the very last line of output**, print the PR URL on its own line. Never wrap the URL in markdown bold, brackets, or any other formatting — output the bare URL so it remains clickable.

### Step 7: Return to original branch

If Step 0 created a new branch (i.e. the original branch was `main`/`master` or a worktree), switch back now:

```bash
git checkout <original-branch>
```

Report this in the summary (e.g. "Switched back to `main`").

If no branch was created in Step 0, skip this step.

## Arguments

If a PR number was provided above, skip Step 1 and start from Step 2 (simplify) using the provided PR number.

## Design principles

- **Review independence**: The review subagent has zero context from code creation. This is intentional — it ensures the reviewer sees the code fresh and can catch issues the author might be blind to.
- **Feedback context**: Addressing feedback keeps full context so decisions about whether to accept or push back on review comments are well-informed.
- **Prompt composability**: Each step delegates to its respective prompt file. Update any individual prompt and the workflow picks up the changes immediately — no need to modify this orchestrator.
