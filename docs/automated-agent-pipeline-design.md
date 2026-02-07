# Automated Agent Pipeline Design

## Overview

A multi-agent development team that operates autonomously through GitHub, handling the full SDLC: triage, story refinement, architecture planning, implementation, code review, and fixes. The human (product owner) reviews escalations and serves as the final code reviewer.

## Architecture

### Workflow Files

Two GitHub Actions workflow files handle all automation:

**`claude-pm.yml`** — The orchestrator. Runs the Product Manager agent.

- Triggers:
  - `schedule: cron '0 6,18 * * *'` (midnight & noon CST / UTC-6)
  - `issues: [opened]` — triage new issues
  - `issue_comment: [created]` — detect agent handbacks and questions
  - `pull_request: [opened, closed, synchronize]` — track PR lifecycle
  - `check_suite: [completed]` — detect CI pass/fail on PRs
  - `issues: [labeled, unlabeled]` — detect when agents remove their labels
- Token: PAT (not `GITHUB_TOKEN`) so that label changes trigger `claude-agents.yml`
- Prompt: Points to `.claude/agents/product-manager.md`

**`claude-agents.yml`** — The dispatch layer. Routes work to specialist agents.

- Triggers: `issues: [labeled]` where label matches `agent/*`
- Reads which `agent/*` label was added, maps to the matching agent definition file
- Token: PAT, so that label removal and comments can trigger PM back
- Prompt template:
  ```
  Read and follow .claude/skills/agent-pipeline/SKILL.md for protocols.
  Read and follow .claude/agents/{agent-name}.md for your role.
  Work on issue #<issue-number>
  ```

Both workflows require permissions: `contents: write`, `pull-requests: write`, `issues: write`, `id-token: write`, `actions: read`.

### Agent Labels

Labels trigger specialist workflows and indicate who currently owns the work:

| Label | Agent | Role |
|-------|-------|------|
| `agent/business-analyst` | Business Analyst | Refines stories, defines acceptance criteria |
| `agent/architect` | Architect | Plans technical approach, selects patterns |
| `agent/backend` | Backend Developer | Implements .NET API code |
| `agent/frontend` | Frontend Developer | Implements React UI code |
| `agent/reviewer` | Code Reviewer | Reviews PRs against project standards |
| `agent/devops` | DevOps Engineer | Infrastructure, GitHub Actions, scripts, deployment |

The Product Manager has no label — it's always watching via its own workflow.

### Project Status Field

Expand the existing GitHub Project Status field to track pipeline phases:

| Status | Meaning |
|--------|---------|
| Triage | New issue, PM is classifying |
| Needs Story | Awaiting BA story refinement |
| Needs Architecture | Awaiting technical planning |
| Ready | Fully specified, ready for implementation |
| Implementing | Code is being written |
| CI Pending | Draft PR open, waiting for CI to pass |
| In Review | CI green, awaiting code review |
| Changes Requested | Review found issues, dev fixing |
| Ready to Merge | Approved + CI green, waiting for owner to merge |
| Awaiting Owner | Blocked on product owner input |
| Done | Complete |

### File Structure

```
.claude/
├── CLAUDE.md                          # Universal project knowledge (tech stack, conventions, build)
├── skills/
│   ├── agent-pipeline/
│   │   └── SKILL.md                   # Pipeline protocol (handoffs, comments, escalation)
│   └── writing-user-stories/
│       └── SKILL.md                   # Story writing guidelines (used by BA)
└── agents/
    ├── product-manager.md             # PM role definition
    ├── business-analyst.md            # BA role definition
    ├── architect.md                   # Architect role definition
    ├── backend.md                     # Backend dev role definition
    ├── frontend.md                    # Frontend dev role definition
    ├── reviewer.md                    # Code reviewer role definition
    └── devops.md                      # DevOps engineer role definition

.github/
└── workflows/
    ├── claude-pm.yml                  # PM orchestrator workflow
    └── claude-agents.yml              # Specialist dispatch workflow
```

### Knowledge Layers

- **`.claude/CLAUDE.md`** — Shared by all Claude interactions (local and GitHub). Tech stack, build commands, code conventions, branching strategy, project management commands.
- **`.claude/skills/agent-pipeline/SKILL.md`** — Pipeline-specific protocol. Only used by GitHub agents. Comment format, handoff rules, label conventions, escalation thresholds, PM status comment format.
- **`.claude/agents/*.md`** — Individual role definitions. Persona, domain expertise, what "done" looks like, constraints on what the agent should NOT do. References the pipeline skill for operational protocols.
- **`.claude/skills/`** — Domain knowledge that specific agents reference (e.g., BA uses writing-user-stories skill).

## Pipeline Flow

### Stage 1 — Triage (Product Manager)

- Trigger: New issue opened, or cron schedule
- PM reads the issue title and body
- Classifies: type (bug, feature, user story, task), priority (P0-P2), size (XS-XL), version label (v1/v2/v3), audience labels (golfers love, course operators love)
- Sets project status to **Triage**
- Creates the PM status comment at the top of the issue (see PM Status Comment below)
- Routes to next phase:
  - Well-defined bug with repro steps → skip to **Ready**
  - Raw idea or vague request → `agent/business-analyst`
  - Already has clear story/acceptance criteria → `agent/architect`
  - Task (infra, scripts, architecture exploration) → `agent/architect` or `agent/devops` depending on scope

### Stage 2 — Story Refinement (Business Analyst)

- Trigger: `agent/business-analyst` label added
- BA reads the issue and follows the `writing-user-stories` skill
- Refines into a proper user story with acceptance criteria
- If unclear, asks clarifying questions (to issue author or escalates to owner)
- When done: posts handback comment, removes own label
- PM picks up, sets status to **Needs Architecture**, adds `agent/architect`

### Stage 3 — Technical Planning (Architect)

- Trigger: `agent/architect` label added
- Architect reads issue, story, reviews codebase structure and existing patterns
- Posts a technical plan comment: approach, files to create/modify, patterns to use, risks
- If blocked, asks questions (routed through PM)
- When done: posts handback comment, removes own label
- PM sets status to **Ready**, assigns appropriate dev agent (`agent/backend`, `agent/frontend`, or both)

### Stage 4 — Implementation (Backend / Frontend / DevOps)

- Trigger: `agent/backend`, `agent/frontend`, or `agent/devops` label added
- Dev agent reads issue, story, and technical plan from comments
- Creates branch: `issue/<number>-description`
- Implements code, runs tests (`make test`), runs lint
- Opens a **draft PR** linking the issue
- Posts handback comment, removes own label
- PM sets status to **CI Pending**

### Stage 5 — CI Gate (Product Manager)

PM monitors the CI pipeline on the draft PR. This stage loops until CI is green.

- CI passes → PM sets status to **In Review**, adds `agent/reviewer`
- CI fails → PM reads the failure logs and routes to the appropriate agent:
  - Build/compile error → back to `agent/backend` or `agent/frontend`
  - Test failure → back to the dev agent that wrote the code
  - Lint failure → back to the dev agent
  - Infrastructure/pipeline issue (workflow config, dependencies, environment) → `agent/devops`
- Agent fixes, pushes to the branch, CI re-runs
- PM re-checks on next event or cron run
- After 3 failed CI cycles, PM escalates: `[Product Manager → @aarongbenjamin] CI failing on PR #XX after 3 fix attempts`

### Stage 6 — Code Review (Reviewer)

- Trigger: `agent/reviewer` label added
- Reviewer reads the PR diff with context about project conventions
- Posts review on the PR
- If approved: posts handback comment, removes label
- If changes requested: posts handback comment, removes label
- PM routes accordingly:
  - Approved + CI green → PM publishes the PR (marks ready for review), enables auto-merge, sets status to **Ready to Merge**, tags owner: `[Product Manager → @aarongbenjamin] PR #XX is ready for your approval`
  - Changes requested → PM sets status to **Changes Requested**, routes back to dev agent

### Stage 7 — Fix & Re-review

- PM re-adds the dev agent label with context about the review feedback
- Dev agent reads review comments, pushes fixes
- CI re-runs (back to Stage 5 CI gate)
- Once CI green again, back to Stage 6 for re-review
- After 3 round-trips without resolution, PM escalates to product owner

### Stage 8 — Approval & Auto-Merge (Product Owner)

- PM publishes the PR with auto-merge enabled
- Product owner reviews and approves the PR
- GitHub auto-merges once approval is given (branch protection ensures CI is already green)
- PM detects the `pull_request: closed` event, sets issue status to **Done**
- No manual merge step — the owner's approval is the merge trigger

## Communication Protocol

### Comment Format

All agent comments are prefixed with the agent's full name in brackets:

- **Status update:** `[Business Analyst] Completed story refinement. Added 5 acceptance criteria.`
- **Question to another agent:** `[Backend → Architect] Should we use a separate aggregate for the waitlist or keep it within the booking aggregate?`
- **Question to product owner:** `[Architect → @aarongbenjamin] Two approaches here — need your input on direction.`
- **Handback to PM:** `[Business Analyst → Product Manager] Story refinement complete. Ready for next phase.`

Every agent comment includes a metadata footer for traceability:

```markdown
[Business Analyst] Story refinement complete. Added 5 acceptance criteria.

---
_Agent: business-analyst · Skills: writing-user-stories, agent-pipeline · Run: [#42](link-to-action-run)_
```

### PM Status Comment

The Product Manager maintains a visible status comment pinned to the top of every active issue:

```markdown
## PM Status
**Phase:** Needs Architecture · **Agent:** architect · **Round-trips:** 1/3

**Summary:** BA completed story refinement. Acceptance criteria defined with
5 scenarios. Waiting for architect to plan technical approach.

**History:**
- PM triaged → priority P1, assigned to BA
- BA refined story, added acceptance criteria (skills: writing-user-stories) · [Run #41](link)
```

Updated by PM on every run. Provides at-a-glance status for the product owner.

### Handoff Protocol

1. Agent completes its work
2. Agent posts a `[Agent Name → Product Manager]` comment summarizing what was done
3. Agent removes its own `agent/*` label
4. PM detects the label removal (event trigger or next cron run)
5. PM updates the project status and PM status comment
6. PM adds the next agent's label (or marks as Done/Awaiting Owner)

Agents never hand off directly to other agents. All routing goes through PM.

### Inter-Agent Questions

When an agent needs input from another specialist:

1. Agent posts `[Agent A → Agent B] question` on the issue
2. PM detects the comment on next run
3. PM routes the question by adding `agent/b` label
4. Agent B answers, hands back to PM
5. PM re-routes to original agent

Counts toward the 3 round-trip limit.

## PM Scheduling & Escalation

### Cron Behavior (Midnight & Noon CST)

- Scan open issues with agent labels — check for stalled work (no comment in 24h)
- Scan issues in "Awaiting Owner" — remind owner if no response in 48h
- Scan open draft PRs — check if review cycle is stuck
- Check backlog ("Ready" status, no agent label) — pick up next highest-priority issue
- Update PM status comments with current state

### Escalation Rules

| Condition | Action |
|-----------|--------|
| 3 round-trips between agents without phase progression | Escalate to product owner |
| Agent hasn't commented in 24h after assignment | PM pings issue, retriggers agent |
| Issue in "Awaiting Owner" for 48h+ | PM posts reminder tagging owner |
| Agent explicitly says it's blocked | Immediate escalation to owner |

### Guardrails

- PM limits concurrent implementation issues to 2-3 (prevents runaway costs)
- PM won't pick up new work if unresolved escalations are waiting on product owner
- PM logs a summary on cron runs so product owner can see overall activity
- Agents must never mark draft PRs as ready — only PM publishes PRs (with auto-merge enabled) when code review approved + CI green
- Branch protection on `main` required: at least 1 approval from product owner, no bypassing allowed, auto-merge enabled
- Product owner's approval triggers auto-merge — no manual merge step needed

## Observability

Three layers of visibility into what the agent team is doing:

### 1. Agent Comment Footers

Every comment posted by an agent includes a metadata footer with the agent name, skills used, and a link to the GitHub Actions run:

```markdown
---
_Agent: business-analyst · Skills: writing-user-stories, agent-pipeline · Run: [#42](link-to-action-run)_
```

Visible directly on the issue without leaving the page.

### 2. PM Status Comment — Agent & Skill History

The PM status comment's History section tracks which agent acted at each phase and which skills were used, with links to the action runs:

```markdown
**History:**
- PM triaged → priority P1, assigned to BA
- BA refined story, added acceptance criteria (skills: writing-user-stories) · [Run #41](link)
- Architect planned approach (skills: agent-pipeline) · [Run #43](link)
```

Gives a full audit trail per issue.

### 3. GitHub Actions Job Summary

Each workflow run writes a structured summary to the Actions tab using `$GITHUB_STEP_SUMMARY`. Visible on the run's page in the Actions UI:

```markdown
## Agent Run Summary
| Field | Value |
|-------|-------|
| Agent | business-analyst |
| Issue | #15 — Tee Time Browsing |
| Phase | Story Refinement |
| Skills | writing-user-stories, agent-pipeline |
| Actions Taken | Refined story, added 5 acceptance criteria, handed back to PM |
| Duration | 2m 34s |
| Outcome | Handback to PM |
```

The workflow prompt instructs agents to write this summary as their final step before exiting.

## Decisions Log

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Trigger mechanism | Label-based | Private repo can't assign `claude[bot]` as user |
| Orchestration model | Hybrid (PM + specialists) | PM as single router, specialists do focused work |
| Agent handoffs | Always through PM | Single source of truth for routing, prevents agent loops |
| PM trigger | Scheduled (2x/day) + event-driven | Matches owner's review cadence (morning + afternoon) |
| Cron schedule | Midnight & noon CST | Owner reviews morning, PM works, owner reviews afternoon |
| Pipeline state tracking | Labels (trigger) + project status (visibility) + PM comment (operational state) | Each layer serves a different purpose |
| Round-trip limit | 3 | Enough for legitimate back-and-forth, prevents infinite loops |
| PR style | Draft PRs | Agents move fast, owner controls what ships |
| Pipeline knowledge | Skill file (not CLAUDE.md) | Isolates GitHub agent behavior from local Claude usage |
| Reviewer role | Review only, no fixes | Clean separation of responsibilities |
| Agent names | Full names (product-manager, business-analyst, etc.) | Clarity in comments and file names |
| Agile ceremonies | Deferred to v2 of the workflow | Get basic pipeline working first |

## Future Enhancements (v2 of Workflow)

- Story points and velocity tracking
- Sprint/iteration planning by PM agent
- Parallel frontend + backend implementation on full-stack features
- Agent performance metrics (cycle time per phase, escalation rate)
