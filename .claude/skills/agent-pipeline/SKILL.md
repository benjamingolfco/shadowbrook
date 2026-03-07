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

**Unchanged:** `owner-gate.yml`, `pr.yml`

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
| Needs Story | BA refining the user story | Planning |
| Story Review | Owner reviewing story | Planning |
| Needs Architecture | Architect doing lightweight review (concerns, notes, points). UX Designer adding UI/UX notes. | Planning |
| Architecture Review | Owner reviewing architectural notes and UI/UX guidance | Planning |
| **Ready** | Fully reviewed. **The sprint gate.** Waits for iteration assignment. | Planning |
| Implementing | In sprint — Architect writes detailed impl plan, then dev agents write code. | Implementation |
| CI Pending | Code pushed, waiting for CI | Implementation |
| In Review | PR open, code review in progress | Implementation |
| Changes Requested | Code review requested changes | Implementation |
| Ready to Merge | CI green + review approved, waiting for owner PR approval | Implementation |
| Awaiting Owner | Blocked on human input — escalation or repeated failures | Either |
| Done | Merged and complete | Implementation |

**Key design points:**
- **Ready** is the sprint gate — issues wait here until assigned to an iteration
- Architecture in planning = **lightweight review** (concerns, patterns, story points)
- Architecture in sprint = **detailed implementation plan** (file-by-file, just-in-time)
- Status IDs for `gh project item-edit` are in CLAUDE.md § GitHub Project Management

---

## Two-Phase Architecture

The Architect's work is split across workflows:

| Phase | Workflow | Depth | Output |
|-------|----------|-------|--------|
| **Architectural Review** | Planning | Lightweight | Technical notes: patterns, data model concerns, API design direction, risks, story points estimate. |
| **Implementation Plan** | Implementation | Detailed | File-by-file plan: what to create, what to modify, exact approach, test strategy. May be written as a plan file on the issue branch. |

**Why split?**
- Lightweight review during planning gives the owner early visibility into technical concerns
- Detailed planning just-in-time in the sprint means plans are always fresh (no staleness)
- Owner can influence direction during Architecture Review before implementation details are locked in

---

## Story Points

The Architect suggests **story points** using the Fibonacci sequence (1, 2, 3, 5, 8, 13, 21) after reviewing each issue's technical complexity. Points are added to the Issue Plan comment.

The planning workflow uses story points for **sprint capacity planning:**
- Each sprint has a **velocity** (total points the team can complete, learned over time)
- When suggesting issues for the next sprint, the planning workflow fills up to the team's velocity
- Points measure relative complexity, not time

---

## Product Owner Review Gates

The pipeline pauses at three checkpoints for product owner review. The manager (Planning Manager or Sprint Manager, depending on phase) sets the appropriate status, assigns the issue to the product owner (`gh issue edit {number} --add-assignee aarongbenjamin`), and tags them with an **Action Required** comment. When the owner responds, the manager unassigns them.

### Gate 1: Story Review

After the BA refines the user story, the Planning Manager sets status to **Story Review** and tags the owner.

- **Owner approves:** Planning Manager advances to **Needs Architecture**.
- **Owner requests changes:** Planning Manager re-dispatches the BA with feedback.

### Gate 2: Architecture Review

After the Architect posts the lightweight technical review (and the UX Designer posts the interaction spec, if dispatched), the Planning Manager sets status to **Architecture Review** and tags the owner.

- **Owner approves:** Planning Manager sets status to **Ready**.
- **Owner requests changes:** Planning Manager re-dispatches the Architect with feedback.

### Gate 3: PR Approval

After CI passes and the code reviewer approves, the Sprint Manager sets status to **Ready to Merge** and tags the owner. The owner reviews the PR on GitHub, approves it, and merges it manually.

**The Sprint Manager must NEVER enable auto-merge or merge the PR. Only the product owner merges.**

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

### Comment Patterns

**1. Action Required — Manager notifying the product owner**

```markdown
### 📋 Planning Manager → @aarongbenjamin

> **Action Required:** Review the user story and comment to approve or request changes.

The BA refined the story with 6 acceptance criteria covering pricing setup, validation, and display.

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

### Technical Review
{architect's lightweight review — concerns, patterns, risks, story points — added during planning}

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
- Owner approved story · [Run #13](link)
- Architect reviewed: 5pt, endpoint extension pattern · [Run #14](link)
- Owner approved architecture, status: Ready · [Run #16](link)
- Sprint dispatched, Architect writing impl plan · [Run #20](link)
- Implementation complete, PR #42 opened · [Run #22](link)
```

### Section Lifecycle

| Phase | What the manager adds to the Issue Plan |
|-------|------------------------------------|
| New (no status) | Create comment with Phase line + History entry. Pin it. |
| Needs Story → Story Review | Add `### Story` section with BA's refined story. Update Phase. |
| Needs Architecture → Architecture Review | Add `### Technical Review` and optionally `### Interaction Spec`. Update Phase. |
| Architecture Review → Ready | Update Phase. Dev Tasks added later during sprint execution. |
| Implementing (sprint start) | Add `### Implementation Plan` with Architect's detailed plan. Add `### Dev Tasks`. Update Phase. |
| Implementing (agents working) | Check off dev task items as agents complete them. |
| CI Pending / In Review / etc. | Update Phase. Add PR link. |
| Done | Update Phase to Done. Final History entry. |

## Handoff Protocol

All routing flows through the managers. Agents **never** hand off directly to other agents.

### Planning Agent Flow (Planning Manager)

```
Planning Manager analyzes event → determines BA/architect/UX needed
  → gathers issue context
  → spawns agent via Task (issue context + specialist instructions)
  → agent returns work product text
  → Planning Manager adds output to Issue Plan comment (appropriate section)
  → Planning Manager updates status, tags owner if entering review gate
  → Planning Manager advances to next phase
```

### Architect + UX Parallel Flow

```
Planning Manager spawns architect → returns lightweight technical review + story points
Planning Manager spawns UX designer → returns interaction spec
Planning Manager merges both outputs:
  → adds Technical Review + Interaction Spec sections to Issue Plan
  → sets status to Architecture Review, tags owner
Owner approves → Planning Manager sets Ready
```

### Sprint Execution Flow (Sprint Manager)

```
Sprint Manager finds unblocked Ready issue in current iteration
  → creates branch (or checks out existing branch)
  → spawns Architect for detailed implementation plan
  → Architect returns file-by-file plan
  → Sprint Manager adds Implementation Plan + Dev Tasks to Issue Plan
  → for each agent (backend → frontend → devops):
      → spawns agent via Task (context + unchecked tasks for this agent)
      → agent implements on the branch, commits, pushes
      → agent returns: files changed, tasks completed, summary
      → Sprint Manager posts handback comment, checks off Dev Tasks
  → creates PR with `agentic` label (or updates existing PR)
  → sets status to CI Pending
```

### Merge Cascade Flow

```
PR merged → Sprint Manager detects
  → verifies linked issue is Done
  → queries: what was this issue blocking?
  → for each blocked sprint issue: check if ALL blockers now Done
  → if unblocked → triggers workflow_dispatch for parallel execution
  → updates Current Sprint Overview
```

### Routing Summary

| Current Phase | Trigger | Action |
|---------------|---------|--------|
| No status (new) | Cron scan | Classify, create Issue Plan, route to Needs Story (Planning) |
| Needs Story | BA returns | Add Story section, set Story Review, tag owner (Planning) |
| Story Review | Owner approves | Spawn Architect (+UX), set Needs Architecture (Planning) |
| Story Review | Owner requests changes | Re-spawn BA with feedback (Planning) |
| Needs Architecture | Architect + UX return | Add Technical Review + Interaction Spec, set Architecture Review, tag owner (Planning) |
| Architecture Review | Owner approves | Set Ready (Planning) |
| Architecture Review | Owner requests changes | Re-spawn Architect with feedback (Planning) |
| Ready (in iteration) | Cron / dependency unblock | Spawn Architect for detailed plan, then implement (Sprint) |
| Implementing | Agents complete | Create/update PR, set CI Pending (Sprint) |
| CI Pending | CI passes | Set In Review (Sprint) |
| CI Pending | CI fails | Re-dispatch agent, set Implementing (Sprint) |
| In Review | Review passes | Set Ready to Merge, tag owner (Sprint) |
| In Review | Review requests changes | Re-dispatch agent, set Changes Requested (Sprint) |
| Changes Requested | Agent fixes | Set CI Pending (Sprint) |
| Ready to Merge | Owner merges | Set Done, trigger merge cascade (Sprint) |

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
| Issue stalled for 48h+ in any review gate | Planning Manager posts Action Required reminder to `@aarongbenjamin` |
| Agent explicitly states it is blocked | Manager immediately escalates with Action Required comment |

## Guardrails

- Agents must **never** merge PRs — including via `gh pr merge`, `gh pr merge --auto`, or any other merge mechanism.
- Agents must **never** enable auto-merge on PRs.
- Agents must **never** submit formal GitHub PR approvals (`gh pr review --approve`).
- Only the **product owner** approves and merges PRs.

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
