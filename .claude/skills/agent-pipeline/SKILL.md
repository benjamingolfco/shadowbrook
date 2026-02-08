---
name: agent-pipeline
description: Shared protocol for the automated multi-agent GitHub pipeline. Defines comment format, handoff rules, escalation thresholds, and observability.
user-invocable: false
---

# Agent Pipeline Protocol

Multi-agent system for automating the Shadowbrook development workflow on GitHub Actions. A **Product Manager (PM) orchestrator** routes work to **specialist agents** via labels, tracks status via GitHub Project fields, and manages state via a PM status comment on each issue.

This skill is the shared contract. Every agent loads it to understand how they communicate, hand off, and escalate.

## Agent Labels

Labels are the routing mechanism. The PM adds a label to assign work; the agent removes it when done. **Only issues with the `agentic` label are processed by the pipeline.** The `agentic` label is added by the product owner to opt an issue into automated management.

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

- **Owner approves:** Comments with approval (e.g., "story approved", "looks good", "approved"). PM advances to **Needs Architecture** and assigns the architect.
- **Owner requests changes:** Comments with feedback. PM sets status back to **Needs Story** and re-assigns the BA with the owner's feedback.

### Gate 2: Architecture Review

After the Architect posts the technical plan, the PM sets status to **Architecture Review** and tags the product owner. The owner reviews the plan for alignment with product goals, scope, and technical direction.

- **Owner approves:** Comments with approval. PM advances to **Ready**.
- **Owner requests changes:** Comments with feedback. PM sets status back to **Needs Architecture** and re-assigns the architect with the owner's feedback.

### Gate 3: PR Approval

After CI passes and the code reviewer approves, the PM publishes the draft PR, sets status to **Ready to Merge**, and tags the product owner. The owner reviews the PR on GitHub and approves it for merge.

- **Owner approves the PR:** GitHub auto-merge completes the squash merge. PM detects the merge and sets status to **Done**.
- **Owner requests changes on the PR:** PM routes back to the implementation agent.

### Detecting Owner Approval

The PM detects owner approval by scanning issue comments for messages from `@aarongbenjamin` (not from a `[bot]` user) on issues in `Story Review` or `Architecture Review` status. The PM interprets the comment as approval or change request based on its content.

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
6. PM determines the next step:
   - **BA hands back** → PM sets status to **Story Review** and tags the product owner for review. Does **not** assign the next agent yet.
   - **Architect hands back** → PM sets status to **Architecture Review** and tags the product owner for review. Does **not** assign the next agent yet.
   - **Owner approves** (on Story Review or Architecture Review) → PM advances to the next phase and assigns the next agent.
   - **Implementation agent hands back** → PM sets status to **CI Pending** and monitors the PR.
   - **Code reviewer hands back** → PM publishes PR if approved, or re-assigns implementation agent if changes requested.
   - Otherwise → sets status to `Done` / `Awaiting Owner` if the pipeline is complete or blocked.

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

## Specialist Agent Workflow

Every specialist agent (BA, Architect, Backend, Frontend, DevOps, Reviewer) follows this workflow when triggered. Agent-specific expertise and implementation details live in the agent file; the process lives here.

### Trigger

You are triggered when the PM adds your `agent/*` label to an issue. This means the PM has assessed the issue and determined it needs your specialty.

### Step 1: Gather Context

1. **Read the issue** — title, body, existing comments, and any linked context (parent epic, related issues). Pay special attention to acceptance criteria and any prior agent comments.
2. **Read the PM status comment** — find the `## PM Status` comment on the issue. Check the current phase, round-trip count, and history to understand where this issue stands in the pipeline.

### Step 2: Execute Your Role

Perform your role-specific work as defined in your agent file. This varies by agent — story refinement, technical planning, code implementation, code review, infrastructure work, etc.

### Step 3: Handle Ambiguity

If requirements, acceptance criteria, or the technical plan are insufficient for your work:

1. Post a directed question using the standard comment format:
   ```
   [Your Agent Name → Target Agent or @aarongbenjamin] Specific question about what is unclear.
   ```
2. Hand back to the PM (see Step 4) so it can route the question appropriately.

**Do not guess at decisions. It is better to escalate than to work based on assumptions.**

### Step 4: Handback

When your work is complete (or you need to escalate), **always** do all three of these:

1. **Post a handback comment** summarizing what you did:
   ```
   [Your Agent Name → Product Manager] Summary of what was done for #{number}.
   ```
   Or if escalating:
   ```
   [Your Agent Name → Product Manager] Cannot proceed — {reason}. Posted questions for {target}.
   ```

2. **Include the metadata footer** on every comment:
   ```
   ---
   _Agent: {agent-name} · Skills: {comma-separated skill list} · Run: [#{run_number}]({run_link})_
   ```
   Build the run link as: `$GITHUB_SERVER_URL/$GITHUB_REPOSITORY/actions/runs/$GITHUB_RUN_ID`

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

Find the `[Architect] Technical plan for #...` comment on the issue. This is your implementation blueprint — follow the file structure, patterns, data model, API design, and testing strategy it defines.

If no technical plan exists (e.g., a well-defined bug or a simple task the PM routed directly to you), use the issue's acceptance criteria and your own codebase exploration to guide implementation.

### Create a Branch

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

### Push and Open a Draft PR

```bash
git push -u origin issue/{number}-{short-description}
gh pr create --draft --label "agentic" --title "{short title}" --body "Closes #{number}

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

Use `gh pr review` to either approve or request changes:

```bash
# To approve:
gh pr review {pr_number} --approve --body "..."

# To request changes:
gh pr review {pr_number} --request-changes --body "..."
```

Then proceed to the standard handback (Step 4 above).

---

## Observability

Three layers provide full traceability from high-level status down to individual actions.

### 1. Comment Footers

Every agent comment includes the metadata footer (see Comment Metadata Footer above) linking back to the GitHub Actions run.

### 2. PM Status Comment

The PM status comment's **History** section accumulates a log of every agent action on the issue, including skills used and run links.

### 3. GitHub Actions Job Summary

Every agent writes a summary table to `$GITHUB_STEP_SUMMARY` as its final step (see Step 5 in Specialist Agent Workflow above).
