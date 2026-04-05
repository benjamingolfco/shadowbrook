# Stress Test Skill Design

**Date:** 2026-04-03
**Status:** Draft

## Overview

A `/stress-test` skill that launches parallel AI agents to exercise the Teeforce system with realistic concurrent usage. One operator agent and N golfer agents interact with the UI via Playwright browsers while an observer agent monitors App Insights for errors, warnings, and flow gaps. The goal is to flush out race conditions, eventual consistency issues, and concurrency bugs — not raw throughput.

## Invocation

```
/stress-test <feature-area> [options]
```

**Arguments:**

| Parameter | Default | Description |
|-----------|---------|-------------|
| `feature-area` | (required) | Feature to test: `waitlist`, `booking`, etc. |
| `--golfers N` | 3 | Number of concurrent golfer agents |
| `--headed` | false | Open visible browsers instead of headless |
| `--env` | `test` | Target environment: `test` or `local` |
| `--timeout M` | 30 | Safety timeout in minutes — kills the run if agents haven't naturally completed |
| `--hint "..."` | none | Natural language instructions to shape agent behavior toward specific contention patterns |

**Environment resolution:**

| Env | Frontend | API |
|-----|----------|-----|
| `test` | `https://purple-field-0a3932a0f.4.azurestaticapps.net` | `https://teeforce-app-test.wittywave-545ed3d5.eastus2.azurecontainerapps.io` |
| `local` | `http://localhost:3000` | `http://localhost:5221` |

## Agent Architecture

Four agent types launched in parallel from a coordinator:

```
┌─────────────────────────────────────┐
│           Coordinator               │
│  (parse args, manage time window)   │
└──────┬──────┬──────────┬────────────┘
       │      │          │
  ┌────▼──┐ ┌─▼────────┐ ┌▼──────────┐
  │Operator│ │Golfer x N│ │ Observer  │
  │(browser│ │(browser  │ │(az CLI    │
  │ login) │ │ anon)    │ │ polling)  │
  └───┬────┘ └────┬─────┘ └─────┬─────┘
      │           │              │
      │     ┌─────▼──────┐      │
      │     │ /dev/sms/* │      │
      │     │ (read SMS) │      │
      │     └────────────┘      │
      │                    ┌────▼──────┐
      │                    │App Insights│
      │                    │  (KQL)    │
      │                    └───────────┘
      ▼           ▼              ▼
  ┌──────────────────────────────────┐
  │     Final Report (markdown)      │
  └──────────────────────────────────┘
```

### Coordinator (main thread)

- Parses arguments, loads credentials from `.local/test-credentials.md`, resolves environment URLs
- Launches all agents in parallel
- Manages a safety timeout (default: 30 minutes) — kills the run if agents haven't naturally completed
- **Live status updates (every ~2 minutes)** — reports to the user on what's happening: which agents are active, notable actions taken, contention events, errors spotted. Keeps the user informed without requiring them to wait for the final report.
- Collects results from all agents when they finish their scenarios or the observer triggers a circuit breaker
- Assembles the final markdown report

### Operator Agent

- **Definition:** `.claude/agents/stress-test-operator.md`
- **Tools:** Playwright MCP tools
- Launches a Playwright browser, logs in with operator test credentials
- Navigates `/operator/*` routes
- Acts autonomously based on the feature area role brief with realistic pacing
- Actively participates throughout the run — managing the tee sheet while golfers interact
- Logs every action with timestamp and result

### Golfer Agents (N instances)

- **Definition:** `.claude/agents/stress-test-golfer.md`
- **Tools:** Playwright MCP tools, Bash (for API calls to dev SMS endpoints)
- Each launches its own Playwright browser
- Uses anonymous public flows: `/join/*`, `/w/*`, `/book/walkup/*`
- Reads SMS via the API directly: `GET {api-base}/dev/sms/golfers/{golferId}` — no need to open the SMS page in browser
- Responds to tee time openings by following links from SMS messages
- When `--hint` is provided, agents bias their behavior accordingly (e.g., all target the same tee time)
- Logs actions and outcomes (success, conflict, error)

### Observer Agent

- **Definition:** `.claude/agents/stress-test-observer.md`
- **Tools:** Bash (for `az monitor app-insights query`)
- Polls App Insights every ~30 seconds using KQL queries from the `app-insights-queries` reference skill
- Watches three categories:
  - **Errors:** unhandled exceptions, 500 responses, failed dependencies
  - **Warnings:** elevated response times, retry storms, concurrency conflicts (409s, optimistic concurrency exceptions)
  - **Flow tracing:** request traces correlated by operation ID to verify end-to-end flow completion
- **Circuit breaker triggers:**
  - Any unhandled exception
  - 3+ failed requests (500) within a single polling window
  - Signals the coordinator, which gracefully shuts down all browser agents

## Agent Safety Rules

All agents (operator, golfer, observer) operate under these hard constraints:

- **NEVER fix issues.** If something is broken, log it and move on. Do not attempt workarounds.
- **NEVER make changes to code.** No edits, no writes, no file modifications. Agents are read-only consumers of the system.
- **NEVER modify infrastructure, configuration, or deployment state.**
- **Report only.** Every agent's job is to exercise the system and document what happens — nothing more.

- **Flag critical blockers immediately.** If an agent encounters something that halts testing — app crash, login failure, page completely unresponsive, environment down — it must signal the coordinator right away so the run can break out early. Don't silently retry or keep going when the system is fundamentally broken.

These rules must be embedded in every agent definition file.

## Agent Behavior Model

Agents are AI with Playwright browsers — they navigate the live UI, read what's on screen, and act. They don't need pre-scripted click paths.

### Feature Area Role Briefs

Each feature area provides intent-level behavior descriptions per actor type. These use terminology consistent with the actual UI (button labels, page titles, menu items).

**`waitlist`:**

| Role | Behaviors |
|------|-----------|
| Operator | Open the walk-up waitlist for the course. Monitor the waitlist for new joins. Post tee time openings to waiting golfers. Close/reopen slots on the tee sheet. |
| Golfer | Join the walk-up waitlist (via `/join` or QR shortcode). Watch for tee time openings via SMS (`/dev/sms/golfers/{id}`). Accept or decline a tee time opening. |

Additional feature areas follow the same pattern and are added to the skill file as the system grows.

### How-To Skills

If how-to skills exist in `.claude/skills/how-tos/` for a flow, agents can reference them for efficiency. They are not required — agents can discover flows by navigating the UI. When an agent successfully completes a flow that has no how-to, it may optionally create one (same pattern as the `local-qa` skill).

### Contention Hints

The `--hint` parameter provides natural language guidance to shape agent behavior toward specific contention patterns:

- `"all golfers target the 9:00 AM slot"` — engineers a race condition on a specific resource
- `"operator closes slots while golfers are booking"` — tests operator/golfer contention
- `"golfers join and leave repeatedly"` — tests rapid state changes

Hints are passed to all agents so they can coordinate their autonomous behavior around the same pressure point.

## KQL Reference Skill

A separate skill at `.claude/skills/app-insights-queries/SKILL.md` containing canned KQL queries organized by category. Used by both the stress test observer and general debugging/investigation.

**Categories:**

- **Exceptions:** Unhandled exceptions in the last N minutes, grouped by type and operation
- **Failed requests:** 4xx/5xx responses by operation name and status code
- **Dependency failures:** Failed calls to SQL, external services
- **Performance:** Slow requests by percentile, response time distribution
- **Traces:** Filtered by severity, OrganizationId (tenant scoping), operation ID correlation
- **Concurrency:** Optimistic concurrency exceptions, 409 Conflict responses

Each query is parameterized (time window, tenant ID) so agents can fill in context-specific values.

## Report Format

Output: `docs/qa/stress-test-{YYYY-MM-DD}-{feature}.md`

```markdown
# Stress Test Report: {feature}
**Date:** {date}  **Duration:** {elapsed}  **Environment:** {env}
**Agents:** 1 operator, {N} golfers, 1 observer
**Hint:** {hint or "none"}
**Exit reason:** completed | circuit-breaker

## Operator Actions
| Time | Action | Result |
|------|--------|--------|

## Golfer Results
| Golfer | Action | Result | Notes |
|--------|--------|--------|-------|

## Contention Events
Races, conflicts, and eventual consistency observations.

## Observer Findings
### Errors
### Warnings
### Flow Gaps
(Missing traces, incomplete operations, orphaned state)

## Summary
{AI-generated summary of findings, contention handling quality, recommendations}
```

## Deliverables

| Artifact | Path |
|----------|------|
| Stress test skill | `.claude/skills/stress-test/SKILL.md` |
| Operator agent definition | `.claude/agents/stress-test-operator.md` |
| Golfer agent definition | `.claude/agents/stress-test-golfer.md` |
| Observer agent definition | `.claude/agents/stress-test-observer.md` |
| KQL reference skill | `.claude/skills/app-insights-queries/SKILL.md` |
