# Standalone Code Reviewer Design

## Summary

Decouple the code reviewer from the agent pipeline so it runs on every PR automatically, not just agentic ones. The reviewer becomes a standalone workflow triggered by PR events, while the PM simplifies by no longer managing reviewer assignment.

## New Reviewer Workflow

A new `claude-code-review.yml` triggers on `pull_request: [opened, synchronize]`. It runs the reviewer agent directly with no labels or pipeline handback.

The reviewer checks whether the PR has the `agentic` label:
- **Agentic PRs:** Uses `--request-changes` to block when issues found, `--comment` when passing
- **Non-agentic PRs:** Always uses `--comment` only (feedback without blocking)

Concurrency: `review-{pr_number}` with `cancel-in-progress: true` — if a new push arrives while reviewing, cancel the stale review and start fresh (the diff changed).

## PM Routing Changes

**Remove:**
- PM no longer adds `agent/reviewer` label when CI passes
- PM no longer handles reviewer handback (label removal detection)

**Keep:**
- **In Review** status still exists — PM sets it when CI passes on agentic PRs for tracking
- PM detects review outcomes via `pull_request_review` events it already listens for

**Updated CI passes flow:**
1. CI passes → PM sets status to **In Review**, updates PM status comment
2. Review happens automatically (no PM action needed)
3. PM detects `pull_request_review` event:
   - Review passes → PM sets **Ready to Merge**, tags owner
   - Review requests changes → PM sets **Changes Requested**, re-assigns implementation agent

## Reviewer Agent Changes

**Remove from `reviewer.md`:**
- `skills: - agent-pipeline` (no longer participates in pipeline handback)
- Handback protocol (no "→ Project Manager" comment, no label removal)

**Keep:**
- `skills: - code-review` (still uses review checklists)
- All review expertise and constraints
- "Check architect's technical plan" behavior (useful context for agentic PRs)

**Add to `reviewer.md`:**
- Check for `agentic` label to decide review mode (request-changes vs comment-only)

## Pipeline Skill & CLAUDE.md Updates

- Remove "Review Agent Workflow" section from `agent-pipeline/SKILL.md`
- Remove `agent/reviewer` from the agent labels table
- Remove 🔍 from role icons table
- Remove `agent/reviewer` from CLAUDE.md labels table
