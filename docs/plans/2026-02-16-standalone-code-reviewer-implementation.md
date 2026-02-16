# Standalone Code Reviewer Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Decouple the code reviewer from the agent pipeline so it runs on every PR automatically, with agentic-aware behavior (request-changes vs comment-only).

**Architecture:** A new `claude-code-review.yml` workflow triggers on PR events. The reviewer agent checks the `agentic` label to decide review mode. The PM no longer assigns the reviewer — it detects review outcomes via `pull_request_review` events.

**Tech Stack:** GitHub Actions YAML, Markdown agent definitions

---

### Task 1: Create the standalone code review workflow

**Files:**
- Create: `.github/workflows/claude-code-review.yml`

**Step 1: Create the workflow file**

```yaml
name: Claude Code Review

on:
  pull_request:
    types: [opened, synchronize]

jobs:
  review:
    # Cancel stale reviews when new commits are pushed
    concurrency:
      group: review-${{ github.event.pull_request.number }}
      cancel-in-progress: true
    runs-on: ubuntu-latest
    permissions:
      contents: read
      pull-requests: write
      issues: read
      id-token: write
      actions: read
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Generate GitHub App token
        id: app-token
        uses: actions/create-github-app-token@v1
        with:
          app-id: ${{ secrets.APP_ID }}
          private-key: ${{ secrets.APP_PRIVATE_KEY }}

      - name: Check if PR is agentic
        id: check-agentic
        env:
          GH_TOKEN: ${{ steps.app-token.outputs.token }}
        run: |
          LABELS=$(gh pr view ${{ github.event.pull_request.number }} --json labels --jq '.labels[].name')
          if echo "$LABELS" | grep -q "^agentic$"; then
            echo "agentic=true" >> $GITHUB_OUTPUT
          else
            echo "agentic=false" >> $GITHUB_OUTPUT
          fi

      - name: Run Code Reviewer
        uses: anthropics/claude-code-action@v1
        with:
          claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          github_token: ${{ steps.app-token.outputs.token }}
          allowed_bots: "benjamingolfco-claude-agent[bot]"
          additional_permissions: |
            actions: read
          claude_args: |
            --model claude-sonnet-4-5-20250929 --allowedTools "Bash" "Read" "Write" "Edit" "Glob" "Grep"
          prompt: |
            Read and follow .claude/agents/reviewer.md for your role.
            Review PR #${{ github.event.pull_request.number }}

            Title: ${{ github.event.pull_request.title }}
            Agentic: ${{ steps.check-agentic.outputs.agentic }}

            If Agentic is true:
            - Use `gh pr review --request-changes` when issues are found
            - Use `gh pr review --comment` when the review passes
            If Agentic is false:
            - Always use `gh pr review --comment` (never request-changes)
```

**Step 2: Commit**

```bash
git add .github/workflows/claude-code-review.yml
git commit -m "feat: add standalone code review workflow for all PRs"
```

---

### Task 2: Update the reviewer agent definition

**Files:**
- Modify: `.claude/agents/reviewer.md`

**Step 1: Remove agent-pipeline skill and update the agent**

Replace the entire file with:

```markdown
---
name: reviewer
description: Code reviewer for Shadowbrook PRs. Reviews for quality, correctness, and adherence to project conventions. Never pushes code.
tools: Read, Glob, Grep, Bash
model: sonnet
memory: project
skills:
  - code-review
---

You are the Code Reviewer for the Shadowbrook tee time booking platform. Your job is to review pull requests for quality, correctness, and adherence to project conventions. You never push code.

You run automatically on every PR — you are NOT part of the agent pipeline and do NOT follow the pipeline handback protocol.

## Expertise

- .NET 10 minimal API patterns and EF Core 10
- React 19 / TypeScript 5.9 component architecture
- Security review (OWASP Top 10)
- Performance analysis (N+1 queries, async patterns, unnecessary allocations)
- Test coverage assessment

## Workflow

1. Read the PR diff thoroughly: `gh pr diff {pr_number}`
2. Use Glob, Grep, and Read to examine surrounding code, related files, and existing patterns. Don't review in isolation.
3. If the PR is linked to an issue with an Architect's technical plan, verify the implementation follows it.
4. Evaluate against the checklists in the **code-review** skill, in order of severity: correctness first, performance last.
5. Post your review based on the agentic mode passed in your prompt:
   - **Agentic PRs:** Use `gh pr review --request-changes` when issues are found, `gh pr review --comment` when passing
   - **Non-agentic PRs:** Always use `gh pr review --comment` (feedback without blocking)

## Constraints

- You **NEVER** push code or commit fixes — you only review
- You **NEVER** merge PRs or mark draft PRs as ready for review
- You **NEVER** submit formal GitHub PR approvals (`--approve`) — only the product owner approves PRs
- You **NEVER** write user stories, plan architecture, or implement features

**After every session**, update your agent memory with:
- PRs reviewed and outcomes
- Common issues encountered
- Patterns that should be documented
```

**Step 2: Commit**

```bash
git add .claude/agents/reviewer.md
git commit -m "refactor: decouple reviewer from agent pipeline, add agentic-aware review modes"
```

---

### Task 3: Update PM routing — remove reviewer assignment

**Files:**
- Modify: `.claude/agents/project-manager.md`

**Step 1: Update CI passes section**

Change the "CI passes" section (around line 222-225) from:

```markdown
### CI passes

1. Set issue status to **In Review**.
2. Add label `agent/reviewer` to the issue.
3. Update PM status comment.
```

to:

```markdown
### CI passes

1. Set issue status to **In Review**.
2. Update PM status comment.

Note: The code reviewer runs automatically on all PRs via a separate workflow. The PM does not assign the reviewer. The PM detects the review outcome via `pull_request_review` events.
```

**Step 2: Update the routing table — remove the In Review → Code Reviewer row**

Change line 138 from:

```markdown
| In Review | Code Reviewer | If approved: see PR Publishing. If changes requested: see Changes Requested. |
```

to:

```markdown
| In Review | — (automatic review) | PM detects `pull_request_review` event. If review passes (comment, no request-changes): see PR Publishing. If review requests changes: set status to **Changes Requested**, re-assign implementation agent. |
```

**Step 3: Commit**

```bash
git add .claude/agents/project-manager.md
git commit -m "refactor: remove reviewer assignment from PM, detect review via PR events"
```

---

### Task 4: Update agent-pipeline skill — remove Review Agent Workflow

**Files:**
- Modify: `.claude/skills/agent-pipeline/SKILL.md`

**Step 1: Remove `agent/reviewer` from the agent labels table**

Remove this row from the table (around line 24):
```markdown
| `agent/reviewer` | Code Reviewer | Reviews PRs against project standards |
```

**Step 2: Remove 🔍 from the role icons table**

Remove this row (around line 94):
```markdown
| 🔍 | Code Reviewer |
```

**Step 3: Remove the "Review Agent Workflow" section**

Delete the entire section from `## Review Agent Workflow` through the `---` separator before `## Observability` (lines 419-452 approximately):

```markdown
## Review Agent Workflow

The Code Reviewer follows a specific workflow between context gathering and handback.

### Find the PR
...
Then proceed to the standard handback (Step 4 above).

---
```

**Step 4: Update the specialist agent list**

Change:
```markdown
Every specialist agent (BA, Architect, UX Designer, Backend Developer, Frontend Developer, DevOps, Reviewer) follows this workflow when triggered.
```
to:
```markdown
Every specialist agent (BA, Architect, UX Designer, Backend Developer, Frontend Developer, DevOps) follows this workflow when triggered.
```

**Step 5: Commit**

```bash
git add .claude/skills/agent-pipeline/SKILL.md
git commit -m "refactor: remove reviewer from pipeline skill (now standalone workflow)"
```

---

### Task 5: Update CLAUDE.md

**Files:**
- Modify: `.claude/CLAUDE.md`

**Step 1: Remove `agent/reviewer` from the labels table**

Remove this row (line 108):
```markdown
| `agent/reviewer` | Assign issue to Code Reviewer agent |
```

**Step 2: Update the Agent Pipeline section to mention the code review workflow**

Change line 117 from:
```markdown
- **Workflow files:** `.github/workflows/claude-pm.yml` (orchestrator), `.github/workflows/claude-agents.yml` (dispatch)
```
to:
```markdown
- **Workflow files:** `.github/workflows/claude-pm.yml` (orchestrator), `.github/workflows/claude-agents.yml` (dispatch), `.github/workflows/claude-code-review.yml` (standalone code review on all PRs)
```

**Step 3: Commit**

```bash
git add .claude/CLAUDE.md
git commit -m "docs: update CLAUDE.md for standalone code reviewer"
```

---

### Task 6: Verify consistency

**Step 1: Search for stale reviewer references**

```bash
grep -r "agent/reviewer" .claude/ .github/ --include="*.md" --include="*.yml"
```

Expected: No matches in active config files (only in docs/plans/ which are historical).

**Step 2: Verify all workflow files reference correct agents**

```bash
ls -la .github/workflows/claude-*.yml
```

Expected: `claude-pm.yml`, `claude-agents.yml`, `claude-code-review.yml`

**Step 3: Fix any remaining references and commit if needed**
