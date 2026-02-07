---
name: agent-pipeline
description: Shared protocol for the automated multi-agent GitHub pipeline. Defines comment format, handoff rules, escalation thresholds, and observability.
user-invocable: false
---

# Agent Pipeline Protocol

Multi-agent system for automating the Shadowbrook development workflow on GitHub Actions. A **Product Manager (PM) orchestrator** routes work to **specialist agents** via labels, tracks status via GitHub Project fields, and manages state via a PM status comment on each issue.

This skill is the shared contract. Every agent loads it to understand how they communicate, hand off, and escalate.

## Agent Labels

Labels are the routing mechanism. The PM adds a label to assign work; the agent removes it when done.

| Label | Agent | Responsibility |
|-------|-------|----------------|
| `agent/business-analyst` | Business Analyst | Refines stories, defines acceptance criteria |
| `agent/architect` | Architect | Plans technical approach, selects patterns |
| `agent/backend` | Backend Developer | Implements .NET API code |
| `agent/frontend` | Frontend Developer | Implements React UI code |
| `agent/reviewer` | Code Reviewer | Reviews PRs against project standards |
| `agent/devops` | DevOps Engineer | Infrastructure, GitHub Actions, scripts, deployment |

The PM has **no label** -- it runs on its own triggers (label changes, cron, workflow dispatch) and is always watching.

## Project Statuses

The PM sets the project status field to reflect where each issue is in the pipeline.

| Status | Meaning |
|--------|---------|
| Triage | New issue, not yet assessed |
| Needs Story | Requires BA refinement before work can begin |
| Needs Architecture | Story is defined; needs technical design |
| Ready | Fully specified and ready for implementation |
| Implementing | An agent is actively writing code |
| CI Pending | Code pushed, waiting for CI to pass |
| In Review | PR open and assigned to code reviewer |
| Changes Requested | Reviewer requested changes; implementation agent re-assigned |
| Ready to Merge | CI green + review approved; PM will publish and auto-merge |
| Awaiting Owner | Blocked on human input from the product owner |
| Done | Merged and complete |

## Comment Format

All agent comments follow a consistent format so humans and the PM can parse them.

**Standard comment:**
```
[Business Analyst] Acceptance criteria added for the booking flow.
```

**Directed message to another agent:**
```
[Backend Developer → Architect] Should we use a separate table for waitlist entries or a status column on bookings?
```

**Escalation to the product owner:**
```
[Architect → @aarongbenjamin] The requirements for walk-up discounts conflict with the cancellation policy. Need a product decision.
```

**Handback to PM:**
```
[Backend Developer → Product Manager] Implementation complete. PR #42 opened with endpoint and tests.
```

## Comment Metadata Footer

Every agent comment ends with a metadata footer for traceability. Construct the run link from GitHub Actions environment variables.

```
---
_Agent: backend-developer · Skills: agent-pipeline, backend-developer · Run: [#N](link)_
```

Build the run link as: `$GITHUB_SERVER_URL/$GITHUB_REPOSITORY/actions/runs/$GITHUB_RUN_ID`

## PM Status Comment

The PM creates and maintains **one status comment** on every active issue. This is the single source of truth for where an issue stands. The PM edits this comment in place (never creates a new one).

```markdown
## PM Status
**Phase:** Implementing · **Agent:** Backend Developer · **Round-trips:** 1/3

**Summary:** Backend agent is building the tee time settings endpoint and tests.

**History:**
- BA refined story, added 5 acceptance criteria (skills: writing-user-stories) · [Run #12](link)
- Architect designed endpoint structure and DB schema (skills: agent-pipeline) · [Run #14](link)
- Backend agent assigned for implementation (skills: agent-pipeline, backend-developer) · [Run #15](link)
```

## Handoff Protocol

All routing flows through the PM. Agents **never** hand off directly to other agents.

1. Agent completes its work.
2. Agent posts a `[Agent Name → Product Manager]` comment summarizing what was done, with the metadata footer.
3. Agent removes its own `agent/*` label from the issue.
4. PM detects the label removal (via event trigger or cron).
5. PM updates the project status field and edits the PM status comment.
6. PM adds the next agent's label -- or sets status to `Done` / `Awaiting Owner` if the pipeline is complete or blocked.

## Inter-Agent Questions

When an agent needs input from another specialist mid-task:

1. Agent posts `[Agent A → Agent B] question` on the issue (e.g., `[Backend Developer → Architect] Should waitlist use a separate table?`).
2. Agent posts `[Agent A → Product Manager]` handback comment and removes its own label.
3. PM detects the handback on its next run.
4. PM adds `agent/b` label to route the question.
5. Agent B answers with a comment, then hands back to PM.
6. PM re-routes to the original agent to continue work.

Each round-trip through PM counts toward the **3 round-trip limit** (see Escalation Rules).

## Escalation Rules

| Condition | Action |
|-----------|--------|
| 3 round-trips between agents on the same issue without phase progression | PM escalates to product owner (`Awaiting Owner`) |
| Agent hasn't commented within 24h of assignment | PM pings the issue and retriggers the agent workflow |
| Issue in `Awaiting Owner` for 48h+ | PM posts a reminder comment tagging `@aarongbenjamin` |
| Agent explicitly states it is blocked | PM immediately escalates to product owner |

## Guardrails

- PM limits concurrent `Implementing` issues to **2-3** to avoid context thrashing.
- PM will **not** pick up new work while unresolved escalations await the product owner.
- Agents must **never** merge PRs.
- Agents must **never** mark draft PRs as ready for review -- only the PM publishes PRs (with auto-merge enabled) when code review is approved and CI is green.
- Only the **product owner** approves PRs for merge.

## Observability

Three layers provide full traceability from high-level status down to individual actions.

### 1. Comment Footers

Every agent comment includes the metadata footer (see Comment Metadata Footer above) linking back to the GitHub Actions run.

### 2. PM Status Comment

The PM status comment's **History** section accumulates a log of every agent action on the issue, including skills used and run links.

### 3. GitHub Actions Job Summary

Every agent writes a summary table to `$GITHUB_STEP_SUMMARY` as its final step:

```markdown
## Agent Run Summary
| Field | Value |
|-------|-------|
| Agent | Backend Developer |
| Issue | #42 -- Tee time settings endpoint |
| Phase | Implementing |
| Skills | agent-pipeline, backend-developer |
| Actions Taken | Created endpoint, service, and 3 integration tests |
| Outcome | Handback to PM |
```
