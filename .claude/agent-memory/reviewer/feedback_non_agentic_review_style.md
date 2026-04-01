---
name: Non-agentic PR review style
description: For non-agentic (human-authored) PRs, always use gh pr review --comment, never --request-changes
type: feedback
---

Non-agentic PRs (human-authored, manual commits) are reviewed with `gh pr review --comment` only — feedback without blocking. Reserve `--request-changes` for agentic PRs where the agent can respond and iterate.

**Why:** The code-review skill explicitly distinguishes between agentic and non-agentic modes. Blocking a human author's PR with `--request-changes` is the wrong tool when the intent is advisory feedback.

**How to apply:** Check how the review was invoked. If the prompt says "non-agentic" or the PR is clearly human-authored without pipeline context, use `--comment` regardless of findings.
