---
name: agent-pipeline
description: Shared protocol for the automated multi-agent GitHub pipeline. Defines comment format, handoff rules, escalation thresholds, and observability.
user-invocable: false
---

# Agent Pipeline Protocol

Multi-agent system for automating the Shadowbrook development workflow on GitHub Actions. A **Project Manager (PM) orchestrator** routes work to **specialist agents** via labels, tracks status via GitHub Project fields, and manages state via a PM status comment on each issue.

This skill is the shared contract. Every agent loads it to understand how they communicate, hand off, and escalate.

## Agent Labels

Labels are the routing mechanism. The PM adds a label to assign work; the agent removes it when done. **Only issues with the `agentic` label are processed by the pipeline.** The `agentic` label is added by the product owner to opt an issue into automated management.

| Label | Agent | Responsibility |
|-------|-------|----------------|
| `agent/business-analyst` | Business Analyst | Refines stories, defines acceptance criteria |
| `agent/architect` | Architect | Plans technical approach, selects patterns |
| `agent/backend-developer` | Backend Developer | Implements .NET API code |
| `agent/frontend-developer` | Frontend Developer | Implements React UI code |
| `agent/ux-designer` | UX Designer | Designs interaction specs for UI stories |
| `agent/reviewer` | Code Reviewer | Reviews PRs against project standards |
| `agent/devops` | DevOps Engineer | Infrastructure, GitHub Actions, scripts, deployment |

The PM has **no label** -- it runs on its own triggers (label changes, cron, workflow dispatch) and is always watching.

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

The pipeline pauses at three checkpoints for product owner review. The PM sets the appropriate status and tags the product owner. The owner signals approval by commenting on the issue.

### Gate 1: Story Review

After the BA refines the user story and acceptance criteria, the PM sets status to **Story Review** and tags the product owner. The owner reviews the story for completeness, correctness, and alignment with product goals.

- **Owner approves:** Comments with approval (e.g., "story approved", "looks good", "approved"). PM advances to **Needs Architecture**. If the story involves UI changes, PM assigns both the Architect (`agent/architect`) and UX Designer (`agent/ux-designer`) in parallel. If backend-only, PM assigns only the Architect.
- **Owner requests changes:** Comments with feedback. PM sets status back to **Needs Story** and re-assigns the BA with the owner's feedback.

### Gate 2: Architecture Review

After the Architect posts the technical plan (and the UX Designer posts the interaction spec, if dispatched), the PM sets status to **Architecture Review** and tags the product owner. The owner reviews the plan (and spec) for alignment with product goals, scope, and technical direction.

- **Owner approves:** Comments with approval. PM advances to **Ready**.
- **Owner requests changes:** Comments with feedback. PM sets status back to **Needs Architecture** and re-assigns the architect with the owner's feedback.

### Gate 3: PR Approval

After CI passes and the code reviewer approves, the PM sets status to **Ready to Merge** and tags the product owner. The owner reviews the PR on GitHub, approves it, and merges it manually.

- **Owner approves and merges the PR:** PM detects the merge and sets status to **Done**.
- **Owner requests changes on the PR:** PM routes back to the implementation agent.

**The PM must NEVER enable auto-merge or merge the PR. Only the product owner merges.**

### Detecting Owner Approval

The PM detects owner approval by scanning issue comments for messages from `@aarongbenjamin` (not from a `[bot]` user) on issues in `Story Review` or `Architecture Review` status. The PM interprets the comment as approval or change request based on its content.

## Comment Format

All agent comments use a structured format with role icons for instant visual recognition and clear action callouts.

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
| 🔍 | Code Reviewer |
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

**2. Handback — agent reporting completion to PM (no action callout)**

Use when handing work back to the PM for routing. No `Action Required` — the PM handles this automatically.

```markdown
### 📝 Business Analyst → Project Manager

Refined user story for #6 with comprehensive acceptance criteria.

**What I did:**
- Expanded from 2 generic items to 6 detailed Given/When/Then scenarios
- Organized by workflow: Setting Pricing, Validation, Viewing
- Kept focus on course operator perspective

---
_Run: [#89](https://github.com/org/repo/actions/runs/12345)_
```

**3. Work Output — substantive deliverable (plan, story, review)**

Use for the actual content an agent produces (technical plans, story refinements, code reviews). These are reference artifacts, not routing messages.

```markdown
### 🏗️ Architect — Technical Plan for #6

## Technical Plan

### Approach
...the actual plan content...

---
_Run: [#93](https://github.com/org/repo/actions/runs/12345)_
```

**4. Routing — PM assigning work to an agent (no action callout)**

Use when the PM routes work to a specialist agent. The agent is triggered by the label, not the comment — the comment is for the audit trail.

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

**5. Question — agent needs clarification before it can proceed**

Use when an agent is blocked and needs input. Direct the question to the appropriate target — the PM will route it and @mention the product owner if needed.

```markdown
### ⚙️ Backend Developer → Architect

> **Question:** Should we allow $0.00 as a valid flat-rate price (free rounds), or require a minimum above zero?

This affects the validation logic in the PUT endpoint. The acceptance criteria say "positive number" but $0 could be intentional for promotional rounds.

---
_Run: [#95](https://github.com/org/repo/actions/runs/12345)_
```

### @mention Rules

- **Only the PM** @mentions the product owner (`@aarongbenjamin`). Specialist agents never @mention anyone — they hand back to the PM, which handles all notifications.
- **Never @mention** agents — they are triggered by labels, not mentions.
- The `> **Action Required:**` callout must appear on every comment where someone needs to act.

### Run Link Footer

Every comment ends with a run link footer for traceability.

**Before posting any comment**, resolve the run link by reading the environment variables into concrete values:

```bash
RUN_ID="$GITHUB_RUN_ID"
RUN_LINK="$GITHUB_SERVER_URL/$GITHUB_REPOSITORY/actions/runs/$GITHUB_RUN_ID"
```

Then use the resolved values in your footer:

```
---
_Run: [#12345](https://github.com/org/repo/actions/runs/12345)_
```

**Never write literal `${GITHUB_RUN_ID}` in comment text.** Always resolve it to the actual number first.

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
2. Agent posts a **Handback** comment (pattern #2 above) summarizing what was done, with the run link footer.
3. Agent removes its own `agent/*` label from the issue.
4. PM detects the label removal (via event trigger or cron).
5. PM updates the project status field and edits the PM status comment.
6. PM determines the next step:
   - **BA hands back** → PM sets status to **Story Review** and tags the product owner for review. Does **not** assign the next agent yet.
   - **Architect hands back** → If UX Designer was also dispatched, PM checks if UX Designer has also handed back. If both done: set status to **Architecture Review** and tag the product owner. If UX still working: update PM status comment, wait. If UX was not dispatched: set status to **Architecture Review** and tag the product owner. Does **not** assign the next agent yet.
   - **UX Designer hands back** → PM checks if Architect has also handed back. If both done: set status to **Architecture Review** and tag the product owner. If Architect still working: update PM status comment, wait. Does **not** assign the next agent yet.
   - **Owner approves** (on Story Review or Architecture Review) → PM advances to the next phase and assigns the next agent.
   - **Implementation agent hands back** → PM sets status to **CI Pending** and monitors the PR.
   - **Code reviewer hands back** → PM publishes PR if approved, or re-assigns implementation agent if changes requested.
   - Otherwise → sets status to `Done` / `Awaiting Owner` if the pipeline is complete or blocked.

## Inter-Agent Questions

When an agent needs input from another specialist mid-task:

1. Agent posts a **Question** comment (pattern #5) directed at the target agent. Do **not** @mention the agent — the PM routes via labels.
2. Agent posts a **Handback** comment (pattern #2) and removes its own label.
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
| Issue in `Awaiting Owner` for 48h+ | PM posts an **Action Required** reminder to `@aarongbenjamin` |
| Agent explicitly states it is blocked | PM immediately escalates with an **Action Required** comment to `@aarongbenjamin` |

## Guardrails

- PM limits concurrent `Implementing` issues to **2-3** to avoid context thrashing.
- PM will **not** pick up new work while unresolved escalations await the product owner.
- Agents must **never** merge PRs — including via `gh pr merge`, `gh pr merge --auto`, or any other merge mechanism.
- Agents must **never** enable auto-merge on PRs.
- Agents must **never** submit formal GitHub PR approvals (`gh pr review --approve`).
- Only the **product owner** approves and merges PRs.

## Specialist Agent Workflow

Every specialist agent (BA, Architect, UX Designer, Backend Developer, Frontend Developer, DevOps, Reviewer) follows this workflow when triggered. Agent-specific expertise and implementation details live in the agent file; the process lives here.

### Trigger

You are triggered when the PM adds your `agent/*` label to an issue. This means the PM has assessed the issue and determined it needs your specialty.

### Step 1: Gather Context

1. **Read the issue** — title, body, existing comments, and any linked context (parent epic, related issues). Pay special attention to acceptance criteria and any prior agent comments.
2. **Read the PM status comment** — find the `## PM Status` comment on the issue. Check the current phase, round-trip count, and history to understand where this issue stands in the pipeline.

### Step 2: Execute Your Role

Perform your role-specific work as defined in your agent file. This varies by agent — story refinement, technical planning, code implementation, code review, infrastructure work, etc.

### Step 3: Handle Ambiguity

If requirements, acceptance criteria, or the technical plan are insufficient for your work:

1. Post a **Question** comment (pattern #5 from Comment Format) directed at the appropriate target. Do not @mention anyone — the PM will handle routing and notifications.
2. Hand back to the PM (see Step 4) so it can route the question appropriately.

**Do not guess at decisions. It is better to escalate than to work based on assumptions.**

### Step 4: Handback

When your work is complete (or you need to escalate), **always** do all three of these:

1. **Post a Handback comment** (pattern #2 from Comment Format) summarizing what you did. Use your role icon and `→ Project Manager` in the heading. Include the run link footer.

2. **If you also produced a deliverable** (technical plan, story refinement, code review), post it as a separate **Work Output** comment (pattern #3) before the handback.

3. **Remove your label** from the issue:
   ```bash
   gh issue edit {number} --remove-label "agent/{your-label}"
   ```

### Step 5: Observability

As your **final step**, write a summary table to `$GITHUB_STEP_SUMMARY`:

```markdown
## Agent Run Summary
| Field | Value |
|-------|-------|
| Agent | {Your Agent Name} |
| Issue | #{number} — {title} |
| Phase | {current pipeline phase} |
| Skills | {comma-separated skill list} |
| Actions Taken | {concise summary of what you did} |
| Outcome | {Handback to PM / Escalated to {target} / Escalated to owner} |
```

---

## Implementation Agent Workflow

Agents that produce code (Backend Developer, Frontend Developer, DevOps Engineer) follow additional steps between context gathering and handback.

### Read the Architect's Plan

Find the `### 🏗️ Architect — Technical Plan` comment on the issue. This is your implementation blueprint — follow the file structure, patterns, data model, API design, and testing strategy it defines.

If no technical plan exists (e.g., a well-defined bug or a simple task the PM routed directly to you), use the issue's acceptance criteria and your own codebase exploration to guide implementation.

### Create a Branch

**Agents must always work on branches and create PRs. Never commit directly to main.**

Use the `issue/<number>-description` convention:
```bash
git checkout -b issue/{number}-{short-description}
```

### Implement, Test, Validate

Follow your agent-specific implementation workflow (defined in your agent file). At a minimum:
1. Explore existing code to match conventions before writing new code
2. Implement the changes
3. Run the relevant test/lint/build commands
4. Fix any failures — iterate until green

### Push and Open a PR

```bash
git push -u origin issue/{number}-{short-description}
gh pr create --label "agentic" --title "{short title}" --body "Closes #{number}

{summary of changes}"
```

Then proceed to the standard handback (Step 4 above).

---

## Review Agent Workflow

The Code Reviewer follows a specific workflow between context gathering and handback.

### Find the PR

Locate the PR linked to the issue:
```bash
gh pr list --search "#{number}" --json number,title,url,headRefName
```

### Read the Diff

Review every changed file thoroughly:
```bash
gh pr diff {pr_number}
```

Use Glob, Grep, and Read to examine surrounding code, related files, and existing patterns. Don't review in isolation — understand how the changes fit into the broader codebase.

### Post Your Review

**Never submit a formal GitHub approval.** Only the product owner approves PRs. Use `gh pr review --request-changes` to formally block a PR when issues are found. When the review passes, post a comment on the PR — do **not** use `--approve`.

```bash
# When issues are found — formally block the PR:
gh pr review {pr_number} --request-changes --body "..."

# When review passes — post a comment only (NO --approve):
gh pr review {pr_number} --comment --body "..."
```

Then proceed to the standard handback (Step 4 above).

---

## Observability

Three layers provide full traceability from high-level status down to individual actions.

### 1. Comment Footers

Every agent comment includes a run link footer (see Run Link Footer above) linking back to the GitHub Actions run.

### 2. PM Status Comment

The PM status comment's **History** section accumulates a log of every agent action on the issue, including skills used and run links.

### 3. GitHub Actions Job Summary

Every agent writes a summary table to `$GITHUB_STEP_SUMMARY` as its final step (see Step 5 in Specialist Agent Workflow above).
