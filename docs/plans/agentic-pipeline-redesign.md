# Agentic Pipeline Redesign — Autonomous Dev Team

## Current State Assessment

The pipeline is well-architected but designed around **owner-in-the-loop** at every stage. That's the core mismatch with your goal. Here's what's working and what's not:

### What's Working
- Two-workflow split (Planning vs Implementation) — good separation of concerns
- Agent specialization (BA, Architect, Frontend, Backend, DevOps, Reviewer) — right roles
- Issue Plan comment as single source of truth — good traceability
- Dependency-driven execution order — smart parallel dispatch
- Merge cascade — automatic unblocking

### What's Not Working (7 Gaps)

#### Gap 1: Too Many Owner Gates (3 → should be ~1)
Currently: Owner reviews at **Story Review**, **Architecture Review**, and **PR Approval** — that's 3 blocking gates per issue. For a 10-issue sprint, that's 30 manual interventions.

**Your goal:** One review at the end of the sprint.

#### Gap 2: No QA Agent
You mentioned wanting a QA tester. Currently the code reviewer checks PR quality, but nobody:
- Writes test plans from acceptance criteria
- Validates that tests actually cover the story
- Does exploratory testing / edge case analysis
- Creates bug issues when things fail

#### Gap 3: Agents Can't Talk to Each Other
Currently: "All routing flows through managers — agents never hand off directly to each other." Every question goes through the manager, which either answers it or escalates to the owner.

**Your goal:** Agents ask questions to each other, get answers, make decisions. The architect can answer the backend dev's question about data modeling. The BA can clarify acceptance criteria for the frontend dev.

#### Gap 4: No Autonomous Decision-Making
Currently: Any ambiguity → escalate to owner. Agents are instructed to be conservative and ask rather than decide.

**Your goal:** Agents make decisions, only escalating truly critical ones (budget, scope changes, security, breaking changes).

#### Gap 5: No Bug Discovery → Fix Loop
You said "make bugs, get them fixed." Currently there's no mechanism for:
- QA finding a bug during testing → creating a bug issue
- Implementation agents discovering a bug in existing code → fixing it or filing it
- A self-healing cycle within the sprint

#### Gap 6: Sprint Planning Requires Owner
Currently: Owner assigns issues to iterations manually. The planning cron suggests issues for the next sprint but waits for owner approval.

**Your goal:** Agents plan sprints autonomously based on priority, velocity, and dependencies.

#### Gap 7: Pipeline is Disabled
Both workflows have all triggers commented out — only `workflow_dispatch` (manual) works. Nothing runs automatically.

---

## Redesigned Pipeline

### Philosophy Change

**Current:** Owner-supervised agents that ask permission at every step.
**New:** Autonomous team that delivers a sprint and presents the result for review.

### New Status Flow

```
(no status) → Needs Story → Needs Architecture → Ready
  → Implementing → Testing → CI Pending → In Review → Sprint Complete
```

**Removed:** Story Review, Architecture Review (owner gates during planning)
**Added:** Testing (QA agent validates before PR)
**Changed:** Ready to Merge → Sprint Complete (batch review, not per-issue)

### Owner Touchpoints (Reduced to 2)

1. **Sprint Kickoff Review** (optional) — Planning Manager posts the sprint plan (issues, points, priorities). Owner can adjust within 24h, otherwise it auto-starts.
2. **Sprint Review** — All PRs are ready. Owner reviews the batch. One sitting, all issues.

Plus: **Critical Escalations** — only for scope changes, security concerns, or repeated failures (3+ CI failures).

### New Agent: QA Tester

```
Role: QA Tester
Responsibilities:
- Review acceptance criteria against implementation
- Verify test coverage (are all AC scenarios tested?)
- Run the test suite and analyze failures
- Exploratory edge case analysis (what did the dev miss?)
- Create bug issues for problems found
- Validate fixes when bugs are resolved
```

The QA agent runs AFTER implementation, BEFORE the PR goes to code review. This creates a dev→QA→fix→QA loop within the sprint.

### Inter-Agent Communication

Instead of all questions going to the owner, agents can consult each other:

```
Backend Dev has a data modeling question
  → Sprint Manager spawns Architect with the question + context
  → Architect answers
  → Sprint Manager passes answer back to Backend Dev (re-dispatch with answer)
```

The Sprint Manager acts as a **message router**, not a **decision bottleneck**. It routes questions to the right agent, not to the owner.

**Escalation ladder:**
1. Agent tries to answer itself (from context)
2. Agent flags question → Manager routes to appropriate peer agent
3. Peer agent answers → work continues
4. If no agent can answer → Manager makes a judgment call
5. Only if truly critical (scope, security, breaking) → escalate to owner

### Autonomous Sprint Planning

The Planning Manager auto-plans sprints:
1. Queries Ready issues by priority (P0 → P1 → P2)
2. Fills to velocity capacity using story points
3. Respects dependencies (blocked issues wait)
4. Assigns to current iteration
5. Posts the sprint plan on the Sprint Overview issue
6. Waits 24h for owner adjustments (or configurable auto-start)
7. If no owner input → auto-starts the sprint

### Bug Discovery Loop

```
QA finds bug → creates bug issue (auto-labeled, auto-prioritized)
  → if sprint-blocking: immediately dispatched to implementation agent
  → if minor: added to backlog for next sprint
  → fix implemented → QA re-validates → cycle completes
```

Implementation agents can also flag bugs they discover in existing code:
- Critical (blocks their work): fix inline, document in PR
- Non-blocking: create a bug issue for the backlog

---

## Revised Sprint Execution Flow

```
Sprint Manager receives Ready issue
  │
  ├─ Step 1: Branch setup
  │
  ├─ Step 2: Architect writes detailed plan
  │    └─ Questions? → Route to BA or make a decision (don't escalate to owner)
  │
  ├─ Step 3: Implementation agents execute
  │    ├─ Backend Dev implements
  │    │    └─ Question about AC? → Route to BA
  │    │    └─ Question about data model? → Route to Architect
  │    ├─ Frontend Dev implements
  │    │    └─ Question about UX? → Route to UX Designer
  │    └─ DevOps implements (if needed)
  │
  ├─ Step 4: QA Testing  ← NEW
  │    ├─ QA reviews implementation against AC
  │    ├─ QA checks test coverage
  │    ├─ QA runs tests, analyzes results
  │    ├─ If bugs found:
  │    │    ├─ Creates bug issues
  │    │    ├─ Re-dispatches implementation agent with bug details
  │    │    └─ Re-validates after fix (max 3 cycles, then escalate)
  │    └─ QA passes → proceed
  │
  ├─ Step 5: Create/update PR
  │    └─ Status: CI Pending
  │
  ├─ Step 6: CI runs
  │    ├─ Pass → Code Review
  │    └─ Fail → Re-dispatch dev agent (max 3 attempts, then escalate)
  │
  ├─ Step 7: Code Review (automated reviewer)
  │    ├─ Pass → mark issue as Sprint Complete
  │    └─ Changes requested → Re-dispatch dev, loop back to QA
  │
  └─ Step 8: Sprint Complete
       └─ All issues done → notify owner for batch review
```

### Sprint Review Flow (Owner's One Touchpoint)

When all sprint issues reach Sprint Complete:

1. Sprint Manager posts a **Sprint Review Summary** on the Sprint Overview issue:
   ```
   ## Sprint Review — Iteration 3

   **Issues Completed:** 8/8
   **Story Points:** 34/34
   **PRs Ready for Review:** #42, #43, #44, #45, #46, #47, #48, #49

   ### Per-Issue Summary
   - #101 — Flat-rate pricing: 5pt · PR #42 · Backend + Frontend
     Key decisions: Used existing endpoint extension pattern. Allowed $0 pricing.
   - #102 — Waitlist notifications: 8pt · PR #43 · Backend + DevOps
     Key decisions: SMS-only (no email). Queue-based processing.
   ...

   ### Decisions Made by Team (no owner input needed)
   - Chose Strategy pattern for pricing variations (Architect)
   - Used optimistic UI updates for waitlist join (Frontend + UX)

   ### Items Needing Your Attention
   - #105 had 2 QA bug cycles — final implementation diverges slightly from AC #3
   - #107 required a new DB migration — review before merging

   @aarongbenjamin — Ready for your review. Approve/merge PRs or comment with feedback.
   ```

2. Owner reviews PRs at their leisure — approve and merge, or comment with feedback.
3. If feedback: Sprint Manager re-dispatches agents, fixes, and re-notifies.

---

## Implementation Plan

### Phase 1: Enable and Simplify (remove owner gates)
- Remove Story Review and Architecture Review gates
- Planning flow becomes: (no status) → Needs Story → Needs Architecture → Ready (no owner stops)
- BA and Architect make decisions autonomously during planning
- Re-enable workflow triggers

### Phase 2: Add QA Agent
- Create `.claude/agents/qa-tester.md`
- Add Testing phase to sprint flow
- Wire QA into Sprint Manager between implementation and PR creation
- Add bug creation capability

### Phase 3: Inter-Agent Communication
- Update Sprint Manager to route questions between agents instead of escalating
- Define the escalation ladder (agent → peer → manager judgment → owner)
- Add consultation protocol to agent instructions

### Phase 4: Autonomous Sprint Planning
- Planning Manager auto-assigns Ready issues to iterations
- 24h grace period for owner adjustments (configurable)
- Auto-start after grace period

### Phase 5: Sprint Review Batching
- Add Sprint Complete status
- Sprint Manager collects all completed issues
- Posts batch review summary
- Owner reviews all PRs in one session

---

## Key Config Changes

### New Status Needed
- `Testing` — between Implementing and CI Pending

### Statuses to Remove/Repurpose
- `Story Review` → removed (no owner gate)
- `Architecture Review` → removed (no owner gate)
- `Ready to Merge` → repurposed as `Sprint Complete`

### New Agent Definition
- `.claude/agents/qa-tester.md`

### Updated Agent Instructions
- All agents: add "you may make reasonable decisions without escalating"
- Sprint Manager: add inter-agent routing, QA phase, sprint review batching
- Planning Manager: remove owner gates, add auto-sprint-planning

### Workflow Changes
- Re-enable all triggers in both workflow files
- Add QA step to sprint execution flow
- Add sprint review notification logic
