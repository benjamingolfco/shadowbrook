---
name: agent-pipeline
description: Shared protocol for the automated multi-agent GitHub pipeline. Defines architecture, comment format, handoff rules, escalation thresholds, and observability.
user-invocable: false
---

# Agent Pipeline Protocol

Multi-agent system for automating the Shadowbrook development workflow on GitHub Actions. The pipeline is split into two workflows — **Planning** (new issues → Ready) and **Implementation** (in-sprint execution).

## Architecture

### Two-Workflow Model

| Workflow | File | Triggers | Concurrency |
|----------|------|----------|-------------|
| **Shadowbrook Planning** | `claude-planning.yml` | issues, issue_comment, schedule (every 6h) | `planning-{issue}`, cancel-in-progress: true |
| **Shadowbrook Implementation** | `claude-implementation.yml` | workflow_dispatch, pull_request, pull_request_review, check_suite, schedule (every 2h) | `sprint-{issue\|pr\|event}`, cancel-in-progress: false |
| **Claude Code Review** | `claude-code-review.yml` | pull_request | `review-{pr}`, cancel-in-progress: true |

**Unchanged:** `pr.yml`

### Agent Responsibility Split

**Agents are pure specialists.** They receive context, do their domain work, and return results. They never interact with GitHub issues directly.

**What planning agents DO:**
- Receive issue context via Task prompt
- Produce their work product (refined story, lightweight technical review, interaction spec)
- Return the work product as text in their Task response

**What implementation agents DO:**
- Receive issue context, detailed implementation plan, and task list via Task prompt
- Write code, run tests, commit, and push to the branch (created by the Sprint Manager)
- Return a summary (files changed, tasks completed)

**What agents DON'T DO:**
- Post comments on issues
- Add or remove labels
- Pin comments
- Write GITHUB_STEP_SUMMARY
- Know about pipeline protocol, comment format, or handoff rules

**The managers (Planning Manager and Sprint Manager) handle ALL GitHub interactions:**
- Formatting and posting comments (with role icons, run link footers)
- Updating the Issue Plan comment
- Managing project status fields
- Writing GITHUB_STEP_SUMMARY
- Managing Sprint Overview issues

### Why Two Workflows?

**Planning** is lightweight and event-driven — runs inline with the Planning Manager, handles story refinement, reviews, and sprint planning. Uses `cancel-in-progress: true` since re-triggering should restart the work.

**Implementation** is long-running — Architect plans, then dev agents write code, create PRs. Uses `cancel-in-progress: false` because canceling mid-implementation loses work. The cron job is a lightweight dispatcher that finds all unblocked sprint issues and triggers a separate `workflow_dispatch` run for each — enabling parallel execution across multiple issues. Merge events also cascade dispatch to newly-unblocked issues.

---

## Opt-Out Model

All issues in the active milestone are managed by default — no opt-in label required. Issues with the `agent/ignore` label are skipped by both workflows.

The `agentic` label is used **only on PRs** — the Sprint Manager adds it when creating PRs (`gh pr create --label agentic`). The owner-gate workflow checks for this label on PRs to require owner approval. Owner's manual PRs don't have the label and auto-pass the gate.

---

## Scoping Hierarchy

```
Milestones   →  What gets planned (roadmap scope)
Iterations   →  What gets implemented (sprint scope)
Dependencies →  Execution order within a sprint
```

- **Milestones** — Planning workflow only processes issues in the active milestone (earliest-due open milestone)
- **Iterations** — GitHub Projects native Iteration field. Implementation only works on current iteration issues.
- **Dependencies** — GitHub native issue dependencies (blocked-by/blocking). Drive execution order automatically.

---

## Project Statuses

Issues with **no status set** are the backlog — new/untouched issues that the planning cron picks up and classifies.

| Status | Meaning | Workflow |
|--------|---------|----------|
| Needs Story | BA refining the user story, Architect doing feasibility check | Planning |
| **Ready** | Planned and estimated. **The sprint gate.** Waits for iteration assignment. | Planning |
| Implementing | In sprint — Architect writes detailed impl plan, dev agents write code, PR lifecycle managed | Implementation |
| Awaiting Owner | Blocked on human input — BA open questions or repeated CI failures | Either |
| QA | Merged — awaiting acceptance criteria validation via `/qa` skill | Implementation |
| Done | Merged to main and complete | Implementation |

**Key design points:**
- **Ready** is the sprint gate — issues wait here until assigned to an iteration
- **No owner review gates** between Needs Story and Ready — the pipeline is autonomous
- Architecture in planning = **feasibility check** (verdict, dependencies, story points)
- Architecture in sprint = **detailed implementation plan** (file-by-file, just-in-time)
- CI, code review, and changes-requested states are tracked by **PR mechanics**, not board statuses
- Status IDs for `gh project item-edit` are in the github-project skill

---

## Two-Phase Architecture

The Architect's work is split across workflows:

| Phase | Workflow | Depth | Output |
|-------|----------|-------|--------|
| **Feasibility Check** | Planning | Lightweight | Verdict (straightforward / structural), dependencies, story points estimate. |
| **Implementation Plan** | Implementation | Detailed | File-by-file plan: what to create, what to modify, exact approach, test strategy. May be written as a plan file on the issue branch. |

**Why split?**
- Feasibility check during planning catches structural concerns early and estimates effort
- Detailed planning just-in-time in the sprint means plans are always fresh (no staleness)
- If the feasibility check flags "structural," the note carries forward to inform the detailed plan

---

## Story Points

The Architect suggests **story points** using the Fibonacci sequence (1, 2, 3, 5, 8, 13, 21) after reviewing each issue's technical complexity. Points are added to the Issue Plan comment.

The planning workflow uses story points for **sprint capacity planning:**
- Each sprint has a **velocity** (total points the team can complete, learned over time)
- When suggesting issues for the next sprint, the planning workflow fills up to the team's velocity
- Points measure relative complexity, not time

---

## Awaiting Owner

The pipeline has **no owner review gates** during normal flow. The only time the pipeline pauses for the owner is when it genuinely needs human input:

- **BA open questions:** The BA flagged something unclear in the story that requires a decision from the owner.
- **CI escalation:** 3+ consecutive CI failures without resolution.
- **Agent blocked:** An agent explicitly states it cannot proceed.

When entering Awaiting Owner, the manager assigns `@aarongbenjamin` and posts an **Action Required** comment. When the owner responds, the manager unassigns them and resumes the pipeline.

---

## Sprint Overview Issues

Two pinned issues, managed by the planning workflow:

### Current Sprint Overview

Shows execution status of the active sprint:

```markdown
## Sprint Overview — Iteration {title}

**Phase:** Active | Complete
**Iteration:** {title} ({start_date} — {end_date})
**Velocity:** {total points} / {capacity}

### Sprint Issues
- #{N} — {story title} · {points}pt · **{status}**

### History
- Sprint started · [Run #N](link)
- #{N} dispatched · [Run #N](link)
- Sprint complete — all issues done · [Run #N](link)
```

### Next Sprint Overview

Planning workspace for the upcoming sprint:

```markdown
## Sprint Overview — Next Sprint

**Phase:** Planning | Review
**Target Iteration:** {title}
**Capacity:** {velocity} points

### Suggested Issues (by priority)
- #{N} — {story title} · {points}pt · Ready ✓

### Backlog Highlights
- {observations about backlog items}

### Questions / Concerns
- {items needing owner input}

### History
- Next sprint planning started · [Run #N](link)
```

---

## Comment Format

All comments posted by the managers (Planning Manager / Sprint Manager) use a structured format with role icons and clear action callouts.

### Role Icons

| Icon | Role |
|------|------|
| 📋 | Planning Manager / Sprint Manager |
| 📝 | Business Analyst |
| 🏗️ | Architect |
| 🎯 | UX Designer |
| ⚙️ | Backend Developer |
| 🎨 | Frontend Developer |
| 🔧 | DevOps Engineer |
| 🔍 | QA Tester |

### Comment Patterns

**1. Action Required — Manager notifying the product owner**

```markdown
### 📋 Planning Manager → @aarongbenjamin

> **Action Required:** The BA flagged open questions on this story that need your input.

The BA refined the story with 6 acceptance criteria but has 2 open questions about scope.

[View the Issue Plan](#link-to-comment)

---
_Run: [#91](https://github.com/org/repo/actions/runs/12345)_
```

**2. Agent Work Output — Manager posting agent's deliverable**

```markdown
### 📝 Business Analyst — Story Refinement for #6

{agent's refined story content}

---
_Run: [#89](https://github.com/org/repo/actions/runs/12345)_
```

**3. Handback — Sprint Manager reporting implementation completion**

```markdown
### ⚙️ Backend Developer → Sprint Manager

Implemented flat-rate pricing feature for #6.

**What was done:**
- Created `src/backend/Shadowbrook.Api/Models/Pricing.cs` with flat-rate entity
- Added PUT/GET endpoints at `/courses/{id}/pricing`

**PR:** #42

---
_Run: [#95](https://github.com/org/repo/actions/runs/12345)_
```

**4. Routing — Manager assigning work**

```markdown
### 📋 Sprint Manager → Backend Developer

Implement the flat-rate pricing feature following the architect's detailed plan.

**Implementation scope:**
- Modify `src/backend/Shadowbrook.Api/Models/Course.cs` to add `FlatRatePrice` property
- Create PUT/GET endpoints at `/courses/{id}/pricing`

---
_Run: [#98](https://github.com/org/repo/actions/runs/12345)_
```

**5. Question Escalation — Manager routing an agent's question**

```markdown
### ⚙️ Backend Developer → @aarongbenjamin

> **Action Required:** Question from the Backend Developer on #{number}.

> **Question:** Should we allow $0.00 as a valid flat-rate price?

---
_Run: [#95](https://github.com/org/repo/actions/runs/12345)_
```

### @mention Rules

- **Only the managers** @mention the product owner (`@aarongbenjamin`). Agents never @mention anyone.
- **Never @mention** agents — they are triggered by workflow dispatch, not mentions.
- The `> **Action Required:**` callout must appear on every comment where someone needs to act.

### Run Link Footer

Every comment ends with a run link footer for traceability:

```
---
_Run: [#12345](https://github.com/org/repo/actions/runs/12345)_
```

**Never write literal `${GITHUB_RUN_ID}` in comment text.** Always use the resolved values provided in the workflow prompt.

## Issue Plan Comment

The Planning Manager creates and maintains **one pinned comment** on every active issue — the Issue Plan. This is the single source of truth for the issue's status, all agent deliverables, and the implementation task list.

### Format

```markdown
## Issue Plan

**Phase:** {current phase} · **Agent:** {current agent or "—"} · **PR:** {#number or "—"}

### Story
{refined story and acceptance criteria from BA — added after BA completes}

### Feasibility
{architect's feasibility check — verdict, dependencies, story points — added during planning}

**Verdict:** straightforward | structural — [reason]
**Dependencies:** [list or "None identified"]
**Story Points:** {N}

### Interaction Spec
{UX designer's spec — added during planning, omit if no UI}

### Implementation Plan
{architect's detailed file-by-file plan — added during sprint execution}

### Dev Tasks
#### Backend Developer
- [ ] Create Tenant entity with org name and contact fields
- [ ] Implement POST /tenants endpoint with validation

#### Frontend Developer
- [ ] Create Tenant TypeScript type and API hooks
- [ ] Build TenantCreate page (registration form)

### History
- Classified as P1/M, routed to BA · [Run #10](link)
- BA refined story with 5 acceptance criteria · [Run #12](link)
- Architect feasibility: straightforward, 5pt · [Run #12](link)
- Status: Ready · [Run #12](link)
- Sprint dispatched, Architect writing impl plan · [Run #20](link)
- Implementation complete, PR #42 opened · [Run #22](link)
```

### Section Lifecycle

| Phase | What the manager adds to the Issue Plan |
|-------|------------------------------------|
| New (no status) | Create comment with Phase line + History entry. Pin it. |
| Needs Story (BA completes) | Add `### Story` section with BA's refined story. Update Phase. |
| Needs Story (Architect completes) | Add `### Feasibility` section with verdict, dependencies, points. Optionally add `### Interaction Spec`. Update Phase. |
| Needs Story → Ready | Update Phase to Ready. Dev Tasks added later during sprint execution. |
| Implementing (sprint start) | Add `### Implementation Plan` with Architect's detailed plan. Add `### Dev Tasks`. Update Phase. |
| Implementing (agents working) | Check off dev task items as agents complete them. Add PR link. |
| QA | Update Phase to QA. History entry for merge. |
| Done | Update Phase to Done after QA passes. Final History entry. |

## Handoff Protocol

All routing flows through the managers. Agents **never** hand off directly to other agents.

### Planning Agent Flow (Planning Manager)

```
Planning Manager analyzes event → determines BA needed
  → gathers issue context
  → spawns BA via Task (issue context + specialist instructions)
  → BA returns refined story
  → Planning Manager updates issue body with refined story
  → Planning Manager adds Story section to Issue Plan
  → if BA flagged open questions → set Awaiting Owner, tag owner, stop
  → spawns Architect for feasibility check (+ UX Designer if UI involved)
  → Architect returns verdict, dependencies, story points
  → Planning Manager adds Feasibility section to Issue Plan
  → Planning Manager sets dependencies on GitHub
  → Planning Manager sets status to Ready
```

### Sprint Execution Flow (Sprint Manager)

```
Sprint Manager finds unblocked Ready issue in current iteration
  → creates issue branch from main (or checks out existing)
  → sets status to Implementing
  → spawns Architect for detailed implementation plan
  → Architect returns file-by-file plan with Dev Tasks
  → Sprint Manager adds Implementation Plan + Dev Tasks to Issue Plan
  → for each agent (backend → frontend → devops):
      → spawns agent via Task (context + unchecked tasks for this agent)
      → agent implements on the branch, commits, pushes
      → agent returns: files changed, tasks completed, summary
      → Sprint Manager posts handback comment, checks off Dev Tasks
  → creates PR targeting main with `agentic` label (or updates existing PR)
  → monitors PR lifecycle (CI, review) — re-dispatches agents as needed
  → on merge: sets status to QA (owner validates via /qa skill)
```

### Merge Cascade Flow

```
PR merged to main → Sprint Manager detects
  → sets linked issue status to QA
  → queries: what was this issue blocking?
  → for each blocked sprint issue: check if ALL blockers now QA or Done
  → if unblocked → triggers workflow_dispatch for parallel execution
  → updates Current Sprint Overview
```

### Routing Summary

| Current Phase | Trigger | Action |
|---------------|---------|--------|
| No status (new) | Cron scan | Classify, create Issue Plan, route to Needs Story (Planning) |
| Needs Story | BA returns (no questions) | Add Story, spawn Architect feasibility check (+UX if UI) (Planning) |
| Needs Story | BA returns (open questions) | Add Story, set Awaiting Owner, tag owner (Planning) |
| Needs Story | Architect returns | Add Feasibility, set dependencies, set Ready (Planning) |
| Awaiting Owner | Owner responds | Unassign owner, resume from where stalled (Either) |
| Ready (in iteration) | Cron / dependency unblock | Set Implementing, create issue branch from main, spawn Architect for detailed plan, implement (Sprint) |
| Implementing | Agents complete | Create/update PR targeting main (Sprint) |
| Implementing | CI fails | Re-dispatch agent with failure details (Sprint) |
| Implementing | Review requests changes | Re-dispatch agent with feedback (Sprint) |
| Implementing | CI + review pass | Set Ready to Merge, owner approves and merges to main (Sprint) |
| Ready to Merge | PR merged | Set QA, trigger merge cascade (Sprint) |

## Inter-Agent Questions

When an agent encounters ambiguity, it includes the question in its Task response. The Planning Manager or Sprint Manager:
- Answers it if possible (from existing context)
- Escalates to product owner if not (posts an Action Required comment)
- Routes to another agent if appropriate (spawns the target agent with the question)

## Escalation Rules

| Condition | Action |
|-----------|--------|
| 3 round-trips between agents on the same issue without phase progression | Manager escalates to product owner with Action Required comment |
| Agent hasn't produced output within expected time | Manager pings and re-spawns the agent |
| Issue stalled for 48h+ in Awaiting Owner | Planning Manager posts Action Required reminder to `@aarongbenjamin` |
| Agent explicitly states it is blocked | Manager immediately escalates with Action Required comment |

## Guardrails

- Agents must **never** merge PRs to main — only the product owner merges.
- The Sprint Manager sets status to **Ready to Merge** after CI + code review pass; the owner approves and merges.
- Agents must **never** submit formal GitHub PR approvals (`gh pr review --approve`).
- The `owner-gate` workflow enforces owner approval as a required status check on agentic PRs.

---

## Dev Tasks Section

The `### Dev Tasks` section of the Issue Plan tracks all implementation work grouped by agent.

### When It's Added

The Sprint Manager adds the Dev Tasks section **during sprint execution** — after the Architect writes the detailed implementation plan. This keeps Dev Tasks fresh and based on the just-in-time plan.

### Rules

- Group tasks by implementation agent (`#### Backend Developer`, `#### Frontend Developer`, `#### DevOps Engineer`).
- Each item should be a concrete, verifiable deliverable.
- The Sprint Manager checks off items as implementation agents complete them.
- Implementation agents must **not** add new items. If scope expands, the Sprint Manager escalates.

---

## UX Designer Output Format

The UX Designer produces an interaction spec with this structure (returned via Task, posted by the Planning Manager):

```markdown
### 🎯 UX Designer — Interaction Spec for #{number}

## User Flow
[Step-by-step description of what the user does and sees]

## Page/Component Breakdown
[What's on the page, how it's laid out, which shadcn/ui components to use]

## States
- **Loading:** [what the user sees while data loads]
- **Empty:** [what the user sees when there's no data]
- **Error:** [what the user sees when something fails]
- **Success:** [confirmation behavior after an action]

## Responsive Behavior
[How the layout adapts from mobile to desktop]

## Accessibility
[Keyboard navigation, focus management, screen reader notes]
```

Omit sections that are not applicable.

---

## Observability

Three layers provide full traceability.

### 1. Comment Footers

Every comment posted by the Planning Manager or Sprint Manager includes a run link footer linking back to the GitHub Actions run.

### 2. Issue Plan Comment

The Issue Plan comment's **History** section accumulates a log of every agent action on the issue, including run links.

### 3. Sprint Overview Issues

Current and Next Sprint Overview issues provide sprint-level visibility into progress, velocity, and blocked items.

### 4. GitHub Actions Job Summary

The Sprint Manager writes a summary table to `$GITHUB_STEP_SUMMARY` after each agent run:

```markdown
## Agent Run Summary
| Field | Value |
|-------|-------|
| Agent | {Agent Name} |
| Issue | #{number} — {title} |
| Phase | {current pipeline phase} |
| Actions Taken | {concise summary} |
| Outcome | {PR opened / Escalated / etc.} |
```
