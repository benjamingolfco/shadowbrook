---
name: agent-pipeline
description: Shared protocol for the automated multi-agent GitHub pipeline. Defines architecture, comment format, handoff rules, escalation thresholds, and observability.
user-invocable: false
---

# Agent Pipeline Protocol

Multi-agent system for automating the Shadowbrook development workflow on GitHub Actions.

## Architecture

### Workflow Model

| Workflow | File | Triggers | Concurrency |
|----------|------|----------|-------------|
| **Shadowbrook Pipeline** | `claude-pipeline.yml` | issues, issue_comment, pull_request, pull_request_review, pull_request_review_comment, check_suite | `pipeline-{issue}`, cancel-in-progress: true |
| **Shadowbrook Implementation** | `claude-implementation.yml` | issues:labeled (backend/frontend/devops only) | `impl-{issue}-{label}`, cancel-in-progress: false |
| **Shadowbrook Cron** | `claude-cron.yml` | schedule (every 6h) | `cron`, cancel-in-progress: true |
| **Claude Code Review** | `claude-code-review.yml` | pull_request | `review-{pr}`, cancel-in-progress: true |

### Agent Responsibility Split

**Agents are pure specialists.** They receive context, do their domain work, and return results. They never interact with GitHub issues directly.

**What planning agents DO:**
- Receive issue context via Task prompt
- Produce their work product (refined story, technical plan, interaction spec)
- Return the work product as text in their Task response

**What implementation agents DO:**
- Receive issue context and task list via Task prompt
- Create branches, write code, run tests, push, open PRs
- Return a summary (PR number, files changed, tasks completed)

**What agents DON'T DO:**
- Post comments on issues
- Add or remove labels
- Pin comments
- Write GITHUB_STEP_SUMMARY
- Know about pipeline protocol, comment format, or handoff rules

**The PM (pipeline/cron workflows) and coordinator (implementation workflow) handle ALL GitHub interactions:**
- Formatting and posting comments (with role icons, run link footers)
- Adding/removing labels
- Pinning comments
- Writing GITHUB_STEP_SUMMARY
- Updating PM status comments
- Managing project status fields

### Why Implementation Needs a Separate Workflow

Implementation agents take 10-20+ minutes. During execution they create PRs — GitHub events that retrigger workflows. With `cancel-in-progress: true`, those events would cancel the running agent. Implementation agents must run in a separate workflow with `cancel-in-progress: false`.

---

## Agent Labels

Labels are the routing mechanism. The PM adds a label to assign work; the PM or coordinator removes it when done. **Only issues with the `agentic` label are processed by the pipeline.** The `agentic` label is added by the product owner to opt an issue into automated management.

| Label | Agent | Responsibility |
|-------|-------|----------------|
| `agent/business-analyst` | Business Analyst | Refines stories, defines acceptance criteria |
| `agent/architect` | Architect | Plans technical approach, selects patterns |
| `agent/backend-developer` | Backend Developer | Implements .NET API code |
| `agent/frontend-developer` | Frontend Developer | Implements React UI code |
| `agent/ux-designer` | UX Designer | Designs interaction specs for UI stories |
| `agent/devops` | DevOps Engineer | Infrastructure, GitHub Actions, scripts, deployment |

For planning agents (BA, Architect, UX Designer), labels serve as **observability markers** — the PM adds the label before spawning the agent via Task, and removes it after posting the agent's output.

For implementation agents (Backend Dev, Frontend Dev, DevOps), labels are **dispatch triggers** — adding the label triggers the implementation workflow.

---

## Project Statuses

The PM sets the project status field to reflect where each issue is in the pipeline.

| Status | Meaning |
|--------|---------|
| Triage | New issue, not yet assessed |
| Needs Story | Requires BA refinement before work can begin |
| Story Review | BA finished; awaiting product owner review of user story and acceptance criteria |
| Needs Architecture | Story approved by owner; needs technical design |
| Architecture Review | Architect finished; awaiting product owner review of technical plan |
| Ready | Plan approved by owner; fully specified and ready for implementation |
| Implementing | An agent is actively writing code |
| CI Pending | Code pushed, waiting for CI to pass |
| In Review | PR open and assigned to code reviewer |
| Changes Requested | Reviewer requested changes; implementation agent re-assigned |
| Ready to Merge | CI green + code review approved; awaiting product owner PR approval |
| Awaiting Owner | Blocked on human input from the product owner |
| Done | Merged and complete |

## Product Owner Review Gates

The pipeline pauses at three checkpoints for product owner review. The PM sets the appropriate status, assigns the issue to the product owner (`gh issue edit {number} --add-assignee aarongbenjamin`), and tags them with an **Action Required** comment. When the owner responds and the issue leaves the review gate, the PM unassigns them (`gh issue edit {number} --remove-assignee aarongbenjamin`). The same assign/unassign pattern applies to **Awaiting Owner** escalations. This lets the owner filter by "assigned to me" to see exactly what needs their attention.

### Gate 1: Story Review

After the BA refines the user story and acceptance criteria, the PM sets status to **Story Review**, assigns the product owner to the issue, and tags them. The owner reviews the story for completeness, correctness, and alignment with product goals.

- **Owner approves:** Comments with approval (e.g., "story approved", "looks good", "approved"). PM advances to **Needs Architecture**. If the story involves UI changes, PM assigns both the Architect (`agent/architect`) and UX Designer (`agent/ux-designer`) in parallel. If backend-only, PM assigns only the Architect.
- **Owner requests changes:** Comments with feedback. PM sets status back to **Needs Story** and re-assigns the BA with the owner's feedback.

### Gate 2: Architecture Review

After the Architect posts the technical plan (and the UX Designer posts the interaction spec, if dispatched), the PM sets status to **Architecture Review**, assigns the product owner, and tags them. The owner reviews the plan (and spec) for alignment with product goals, scope, and technical direction.

- **Owner approves:** Comments with approval. PM advances to **Ready**.
- **Owner requests changes:** Comments with feedback. PM sets status back to **Needs Architecture** and re-assigns the architect with the owner's feedback.

### Gate 3: PR Approval

After CI passes and the code reviewer approves, the PM sets status to **Ready to Merge**, assigns the product owner, and tags them. The owner reviews the PR on GitHub, approves it, and merges it manually.

- **Owner approves and merges the PR:** PM detects the merge and sets status to **Done**.
- **Owner requests changes on the PR:** PM routes back to the implementation agent.

**The PM must NEVER enable auto-merge or merge the PR. Only the product owner merges.**

### Detecting Owner Approval

The PM detects owner approval by scanning issue comments for messages from `@aarongbenjamin` (not from a `[bot]` user) on issues in `Story Review` or `Architecture Review` status. The PM interprets the comment as approval or change request based on its content.

## Comment Format

All comments posted by the PM/coordinator use a structured format with role icons for instant visual recognition and clear action callouts.

### Role Icons

Every comment heading starts with the agent's role icon:

| Icon | Role |
|------|------|
| 📋 | Project Manager |
| 📝 | Business Analyst |
| 🏗️ | Architect |
| 🎯 | UX Designer |
| ⚙️ | Backend Developer |
| 🎨 | Frontend Developer |
| 🔧 | DevOps Engineer |

### Comment Patterns

**1. Action Required — PM notifying the product owner (PM only)**

Used exclusively by the PM when the product owner needs to take action. The `> **Action Required**` callout and `@aarongbenjamin` @mention must be present so the owner gets notified.

```markdown
### 📋 Project Manager → @aarongbenjamin

> **Action Required:** Review the user story and comment to approve or request changes.

The BA refined the story with 6 acceptance criteria covering pricing setup, validation, and display.

[View the BA's story refinement](#link-to-comment)

---
_Run: [#91](https://github.com/org/repo/actions/runs/12345)_
```

**2. Agent Work Output — PM posting agent's deliverable**

Used by the PM/coordinator when posting an agent's work product (technical plan, story refinement, interaction spec). The PM formats the output with the agent's role icon.

```markdown
### 📝 Business Analyst — Story Refinement for #6

{agent's refined story content}

---
_Run: [#89](https://github.com/org/repo/actions/runs/12345)_
```

```markdown
### 🏗️ Architect — Technical Plan for #6

{agent's technical plan content}

---
_Run: [#93](https://github.com/org/repo/actions/runs/12345)_
```

**3. Handback — coordinator reporting implementation completion**

Used by the implementation coordinator when an implementation agent finishes.

```markdown
### ⚙️ Backend Developer → Project Manager

Implemented flat-rate pricing feature for #6.

**What was done:**
- Created `src/api/Models/Pricing.cs` with flat-rate entity
- Added PUT/GET endpoints at `/courses/{id}/pricing`
- Wrote 4 integration tests

**PR:** #42

---
_Run: [#95](https://github.com/org/repo/actions/runs/12345)_
```

**4. Routing — PM assigning work to an implementation agent**

Used when the PM routes work to an implementation agent. The agent is triggered by the label, not the comment — the comment is for the audit trail.

```markdown
### 📋 Project Manager → Backend Developer

Owner approved the technical plan. Implement the flat-rate pricing feature following the architect's design.

**Implementation scope:**
- Modify `src/api/Models/Course.cs` to add `FlatRatePrice` property
- Create PUT/GET endpoints at `/courses/{id}/pricing`

See the [Architect's technical plan](#link-to-comment) for full details.

---
_Run: [#98](https://github.com/org/repo/actions/runs/12345)_
```

**5. Question Escalation — PM routing an agent's question**

Used when an agent returned a question in its Task response that the PM needs to route.

```markdown
### ⚙️ Backend Developer → Architect

> **Question:** Should we allow $0.00 as a valid flat-rate price (free rounds), or require a minimum above zero?

This affects the validation logic in the PUT endpoint. The acceptance criteria say "positive number" but $0 could be intentional for promotional rounds.

---
_Run: [#95](https://github.com/org/repo/actions/runs/12345)_
```

### @mention Rules

- **Only the PM** @mentions the product owner (`@aarongbenjamin`). Agents never @mention anyone.
- **Never @mention** agents — they are triggered by labels, not mentions.
- The `> **Action Required:**` callout must appear on every comment where someone needs to act.

### Run Link Footer

Every comment ends with a run link footer for traceability. The PM and coordinator receive the Run ID and Run Link as workflow context variables and use them directly.

```
---
_Run: [#12345](https://github.com/org/repo/actions/runs/12345)_
```

**Never write literal `${GITHUB_RUN_ID}` in comment text.** Always use the resolved values provided in the workflow prompt.

## Pinned Comments

Two comments are **pinned** to every active issue for easy access. Pinning ensures they appear prominently and are not buried in the comment thread.

Use the "Pin issue comment" command from CLAUDE.md § GitHub Project Management. Pinning is idempotent — calling it on an already-pinned comment is safe. The PM/coordinator must pin a comment immediately after creating it.

### 1. PM Status Comment (`## PM Status`)

Created and pinned by the PM. Single source of truth for pipeline phase, agent assignment, and history.

### 2. Dev Task List (`## Dev Task List`)

Created and pinned by the PM **after the owner approves the architecture**. The PM extracts tasks from the architect's plan and UX spec into a structured checklist grouped by agent. The PM reads this to know which agents to dispatch and in what order. The coordinator checks off items as implementation agents complete work.

## PM Status Comment

The PM creates and maintains **one status comment** on every active issue. This is the single source of truth for where an issue stands. The PM edits this comment in place (never creates a new one). **The PM must pin this comment after creating it.**

```markdown
## PM Status
**Phase:** Implementing · **Agent:** Backend Developer · **Round-trips:** 1/3

**Summary:** Backend agent is building the tee time settings endpoint and tests.

**History:**
- BA refined story, added 5 acceptance criteria · [Run #12](link)
- Architect designed endpoint structure and DB schema · [Run #14](link)
- Backend agent assigned for implementation · [Run #15](link)
```

## Handoff Protocol

All routing flows through the PM. Agents **never** hand off directly to other agents.

### Planning Agent Flow (single PM run)

```
PM analyzes event → determines BA/architect/UX needed
  → adds agent/{name} label (observability)
  → gathers issue context
  → spawns agent via Task (issue context + specialist instructions)
  → agent returns work product text
  → PM formats as comment (role icon, run link footer)
  → PM posts comment, pins if needed, removes label, updates status
  → PM advances to next phase
```

### Architect + UX Parallel Flow

```
PM spawns architect → returns technical plan
PM spawns UX designer → returns interaction spec
PM merges both outputs:
  → posts technical plan comment
  → posts interaction spec comment
  → sets status to Architecture Review, tags owner
Owner approves → PM creates Dev Task List (from plan + spec), pins it, sets Ready
```

### Implementation Agent Flow (separate workflow)

```
Coordinator gathers context (issue body, technical plan, dev task list items)
  → spawns implementation agent via Task (context + tasks to implement)
  → agent creates branch, implements, tests, pushes, opens PR
  → agent returns: PR number, files changed, tasks completed, summary
  → coordinator formats handback comment (role icon, run link)
  → coordinator posts comment, checks off dev task items, removes label
  → coordinator writes GITHUB_STEP_SUMMARY
```

### Routing Summary

| Current Phase | Trigger | PM/Coordinator Action |
|---------------|---------|----------------------|
| Needs Story | BA returns via Task | Post story comment, set Story Review, tag owner |
| Story Review | Owner approves | Spawn architect (+ UX if UI), set Needs Architecture |
| Story Review | Owner requests changes | Spawn BA again with feedback |
| Needs Architecture | Architect + UX return | Post plan + spec comments, set Architecture Review, tag owner |
| Architecture Review | Owner approves | Create Dev Task List (from plan + spec), pin it, set Ready, add implementation agent label |
| Ready | — | Add implementation agent label, set Implementing |
| Implementing | Impl agent returns | Post handback, check off tasks, dispatch next agent or set CI Pending |
| CI Pending | CI passes | Set In Review |
| CI Pending | CI fails | Add implementation agent label, set Implementing |
| In Review | Review passes | Set Ready to Merge, tag owner |
| In Review | Review requests changes | Add implementation agent label, set Changes Requested |
| Changes Requested | Impl agent returns | Set CI Pending |

## Inter-Agent Questions

When an agent encounters ambiguity, it includes the question in its Task response. The PM/coordinator:
- Answers it if possible (from existing context)
- Escalates to product owner if not (posts an Action Required comment)
- Routes to another agent if appropriate (spawns the target agent with the question)

Each round-trip through PM counts toward the **3 round-trip limit** (see Escalation Rules).

## Escalation Rules

| Condition | Action |
|-----------|--------|
| 3 round-trips between agents on the same issue without phase progression | PM escalates to product owner (`Awaiting Owner`), assigns them to the issue |
| Agent hasn't commented within 24h of assignment | PM pings the issue and retriggers the agent workflow |
| Issue in `Awaiting Owner` for 48h+ | PM posts an **Action Required** reminder to `@aarongbenjamin` |
| Agent explicitly states it is blocked | PM immediately escalates with an **Action Required** comment to `@aarongbenjamin` |

## Guardrails

- Agents must **never** merge PRs — including via `gh pr merge`, `gh pr merge --auto`, or any other merge mechanism.
- Agents must **never** enable auto-merge on PRs.
- Agents must **never** submit formal GitHub PR approvals (`gh pr review --approve`).
- Only the **product owner** approves and merges PRs.
- PM will **not** pick up new work while unresolved escalations await the product owner.

---

## Dev Task List

The Dev Task List is a pinned comment on the issue that tracks all implementation work grouped by agent. It serves as the contract between the architect's plan and the PM's routing logic.

### Format

```markdown
## Dev Task List

### Backend Developer
- [ ] Create Tenant entity with org name and contact fields
- [ ] Add required TenantId FK to Course entity
- [ ] Implement POST /tenants endpoint with validation
- [ ] Write integration tests for all tenant endpoints

### Frontend Developer
- [ ] Create Tenant TypeScript type and API hooks
- [ ] Build TenantCreate page (registration form)
- [ ] Build TenantList page (list view with course counts)
- [ ] Add routes for /admin/tenants
```

### Who Creates It

The **PM** creates the Dev Task List **after the owner approves the architecture** (not before). This avoids creating a task list that gets thrown away if the owner requests changes. The PM extracts backend tasks from the Architect's plan and frontend tasks from the UX Designer's spec, combines them into a single comment, and pins it.

### Rules

- Group tasks by implementation agent (`### Backend Developer`, `### Frontend Developer`, `### DevOps Engineer`).
- Each item should be a concrete, verifiable deliverable — not a vague description.
- The coordinator checks off items as implementation agents complete them.
- Implementation agents must **not** add new items. If scope expands, the coordinator escalates to the PM.
- The PM reads the dev task list after each implementation handback to determine what to dispatch next.

---

## UX Designer Output Format

The UX Designer produces an interaction spec with this structure (returned via Task, posted by PM):

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

Three layers provide full traceability from high-level status down to individual actions.

### 1. Comment Footers

Every comment posted by PM/coordinator includes a run link footer linking back to the GitHub Actions run.

### 2. PM Status Comment

The PM status comment's **History** section accumulates a log of every agent action on the issue, including run links.

### 3. GitHub Actions Job Summary

The implementation coordinator writes a summary table to `$GITHUB_STEP_SUMMARY` after each agent run:

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
