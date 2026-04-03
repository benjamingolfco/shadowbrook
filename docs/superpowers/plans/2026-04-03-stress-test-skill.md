# Stress Test Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a `/stress-test` skill that launches parallel AI agents (operator, golfers, observer) to exercise the system with realistic concurrent usage and surface race conditions, eventual consistency issues, and concurrency bugs.

**Architecture:** A user-invocable skill acts as coordinator, dispatching three agent types in parallel via the Agent tool — an operator with a Playwright browser, N golfers with Playwright browsers using anonymous flows, and an observer polling App Insights via `az` CLI. The coordinator provides live status updates every ~2 minutes and assembles a final markdown report.

**Tech Stack:** Claude Code skills/agents, Playwright MCP tools, Azure CLI (`az monitor app-insights query`), KQL

**Spec:** `docs/superpowers/specs/2026-04-03-stress-test-skill-design.md`

---

### Task 1: KQL Reference Skill

**Files:**
- Create: `.claude/skills/app-insights-queries/SKILL.md`

This is a standalone reference skill with canned KQL queries. Used by the observer agent and available for general debugging.

- [ ] **Step 1: Create the skill file**

```markdown
---
name: app-insights-queries
description: Canned KQL queries for Azure Application Insights. Use when debugging errors, investigating performance, or monitoring the system via az CLI.
---

# App Insights KQL Queries

Reference queries for Azure Application Insights. Run via `az monitor app-insights query`.

## Prerequisites

- Must be logged in to Azure CLI: `az login`
- App Insights app name: `shadowbrook-appinsights-test` (test env)
- Resource group: `shadowbrook-rg-test` (test env)

## Running Queries

```bash
az monitor app-insights query \
  --app shadowbrook-appinsights-test \
  --resource-group shadowbrook-rg-test \
  --analytics-query "{KQL}" \
  --offset {timespan}
```

The `--offset` parameter filters to recent data (e.g., `30m`, `1h`, `6h`).

## Queries

### Exceptions — Unhandled exceptions grouped by type and operation

```kql
exceptions
| where timestamp > ago({timespan})
| summarize count() by type, outerMessage, operation_Name
| order by count_ desc
```

### Failed Requests — 4xx/5xx responses by operation and status code

```kql
requests
| where timestamp > ago({timespan})
| where success == false
| summarize count() by operation_Name, resultCode
| order by count_ desc
```

### Dependency Failures — Failed calls to SQL, external services

```kql
dependencies
| where timestamp > ago({timespan})
| where success == false
| summarize count() by type, target, name, resultCode
| order by count_ desc
```

### Slow Requests — Response time percentiles by operation

```kql
requests
| where timestamp > ago({timespan})
| summarize p50=percentile(duration, 50), p95=percentile(duration, 95), p99=percentile(duration, 99), count() by operation_Name
| order by p95 desc
```

### Traces by Severity — Warnings and errors

```kql
traces
| where timestamp > ago({timespan})
| where severityLevel >= 2
| summarize count() by severityLevel, message
| order by count_ desc
```

Severity levels: 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical

### Traces by Tenant — Filtered by OrganizationId

```kql
traces
| where timestamp > ago({timespan})
| where customDimensions.OrganizationId == "{organizationId}"
| order by timestamp desc
| project timestamp, severityLevel, message, operation_Name
```

### Concurrency — Optimistic concurrency exceptions and 409 Conflict responses

```kql
union
  (exceptions
  | where timestamp > ago({timespan})
  | where type contains "DbUpdateConcurrencyException" or type contains "ConcurrencyException"
  | project timestamp, source="exception", detail=outerMessage, operation_Name),
  (requests
  | where timestamp > ago({timespan})
  | where resultCode == "409"
  | project timestamp, source="409_response", detail=operation_Name, operation_Name)
| order by timestamp desc
```

### Request Volume — Requests per minute for load monitoring

```kql
requests
| where timestamp > ago({timespan})
| summarize count() by bin(timestamp, 1m), operation_Name
| order by timestamp desc
```

### End-to-End Flow — Correlated traces by operation ID

```kql
union requests, dependencies, traces, exceptions
| where timestamp > ago({timespan})
| where operation_Id == "{operationId}"
| order by timestamp asc
| project timestamp, itemType, name, message, resultCode, success, duration
```

### Error Spike Detection — Errors per minute (for circuit breaker logic)

```kql
requests
| where timestamp > ago({timespan})
| where success == false and toint(resultCode) >= 500
| summarize errorCount=count() by bin(timestamp, 1m)
| order by timestamp desc
```
```

- [ ] **Step 2: Verify the file renders correctly**

Read the file back and confirm all KQL blocks are properly formatted and no syntax issues.

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/app-insights-queries/SKILL.md
git commit -m "feat: add App Insights KQL reference skill"
```

---

### Task 2: Observer Agent Definition

**Files:**
- Create: `.claude/agents/stress-test-observer.md`

The observer polls App Insights and reports findings. No browser — Bash only.

- [ ] **Step 1: Create the agent definition**

```markdown
---
name: stress-test-observer
description: Monitors Azure Application Insights during stress tests. Polls for errors, warnings, and flow gaps. Triggers circuit breaker on critical failures.
tools: Read, Bash, Grep, Glob
model: sonnet
---

# Stress Test Observer

You monitor Application Insights during a stress test run, watching for errors, performance issues, and flow gaps. You poll on a regular interval and report findings.

## Safety Rules

- **NEVER fix issues.** If something is broken, log it. Do not attempt workarounds.
- **NEVER make changes to code.** No edits, no writes, no file modifications.
- **NEVER modify infrastructure, configuration, or deployment state.**
- **Report only.** Your job is to watch and document — nothing more.
- **Flag critical blockers immediately.** If you encounter something that halts testing — environment down, App Insights unreachable, Azure CLI auth expired — report it as a CRITICAL BLOCKER in your output so the coordinator can halt the run.

## Inputs

You will receive:
- `app_name` — App Insights resource name
- `resource_group` — Azure resource group
- `time_offset` — How far back to look (e.g., `5m`)
- `poll_interval_seconds` — How often to poll (default: 30)
- `run_duration_hint` — Approximate expected run duration, so you know when to wrap up

## Process

1. **Verify connectivity:**
   ```bash
   az monitor app-insights query --app {app_name} --resource-group {resource_group} --analytics-query "requests | take 1" --offset 1h
   ```
   If this fails, report CRITICAL BLOCKER immediately.

2. **Poll loop** — every `poll_interval_seconds`:

   a. Run the **Error Spike Detection** query (see `.claude/skills/app-insights-queries/SKILL.md`):
   ```bash
   az monitor app-insights query --app {app_name} --resource-group {resource_group} --analytics-query "requests | where timestamp > ago(2m) | where success == false and toint(resultCode) >= 500 | summarize errorCount=count() by bin(timestamp, 1m) | order by timestamp desc" --offset 5m
   ```

   b. Run the **Exceptions** query:
   ```bash
   az monitor app-insights query --app {app_name} --resource-group {resource_group} --analytics-query "exceptions | where timestamp > ago(2m) | summarize count() by type, outerMessage, operation_Name | order by count_ desc" --offset 5m
   ```

   c. Run the **Concurrency** query:
   ```bash
   az monitor app-insights query --app {app_name} --resource-group {resource_group} --analytics-query "union (exceptions | where timestamp > ago(2m) | where type contains 'DbUpdateConcurrencyException' or type contains 'ConcurrencyException' | project timestamp, source='exception', detail=outerMessage, operation_Name), (requests | where timestamp > ago(2m) | where resultCode == '409' | project timestamp, source='409_response', detail=operation_Name, operation_Name) | order by timestamp desc" --offset 5m
   ```

   d. **Circuit breaker check** — after each poll:
      - If any unhandled exception is found → report `CIRCUIT_BREAKER: unhandled exception detected`
      - If 3+ server errors (500+) in a single 1-minute bucket → report `CIRCUIT_BREAKER: error spike detected`
      - Otherwise, accumulate findings silently

3. **Final summary** — when the run ends (or circuit breaker fires), compile all findings.

## Output Format

Return this structure:

```markdown
## Observer Report

**Polling duration:** {start} — {end}
**Polls completed:** {N}
**Circuit breaker:** {triggered | not triggered}

### Errors
| Time | Type | Message | Operation |
|------|------|---------|-----------|
(table of all exceptions found, or "None")

### Failed Requests
| Time | Operation | Status Code | Count |
|------|-----------|-------------|-------|
(table of 4xx/5xx, or "None")

### Concurrency Events
| Time | Source | Detail | Operation |
|------|--------|--------|-----------|
(table of concurrency exceptions and 409s, or "None")

### Warnings
- {elevated response times, retry patterns, anything unusual}

### Flow Gaps
- {missing traces, incomplete operations, orphaned state — or "None detected"}

### Circuit Breaker Detail
{If triggered: what triggered it, the exact error, and the timestamp. If not: "Not triggered."}
```

## Guidelines

- Keep polls lightweight. Run only the queries needed, not every query in the reference skill.
- If Azure CLI auth fails mid-run, report it as a CRITICAL BLOCKER — don't retry silently.
- Concurrency events (409s, DbUpdateConcurrencyException) are expected during stress tests. Report them as data, not as errors. They only become circuit-breaker triggers if accompanied by 500s.
- Note App Insights ingestion delay — data may appear 1-2 minutes after the event. Account for this in your time windows.
```

- [ ] **Step 2: Verify the file renders correctly**

Read the file back and confirm formatting, frontmatter, and all query blocks are correct.

- [ ] **Step 3: Commit**

```bash
git add .claude/agents/stress-test-observer.md
git commit -m "feat: add stress test observer agent definition"
```

---

### Task 3: Operator Agent Definition

**Files:**
- Create: `.claude/agents/stress-test-operator.md`

The operator agent logs in and interacts with the operator UI.

- [ ] **Step 1: Create the agent definition**

```markdown
---
name: stress-test-operator
description: Course operator agent for stress tests. Logs in and manages the tee sheet, waitlist, and course operations via Playwright browser.
tools: Read, Bash, Grep, Glob
model: sonnet
---

# Stress Test Operator

You are a course operator exercising the Shadowbrook operator UI during a stress test. You log in with test credentials and perform realistic operator actions via a Playwright browser.

## Safety Rules

- **NEVER fix issues.** If something is broken, log it and move on. Do not attempt workarounds.
- **NEVER make changes to code.** No edits, no writes, no file modifications.
- **NEVER modify infrastructure, configuration, or deployment state.**
- **Report only.** Your job is to exercise the system and document what happens — nothing more.
- **Flag critical blockers immediately.** If you encounter something that halts testing — app crash, login failure, page completely unresponsive, environment down — report it as a CRITICAL BLOCKER in your output so the coordinator can halt the run.

## Inputs

You will receive:
- `frontend_url` — The base URL for the web app
- `credentials` — Operator test account credentials (UPN + password)
- `feature_area` — Which feature to exercise (e.g., `waitlist`)
- `role_brief` — Specific behaviors to perform
- `hint` — Optional contention hint to shape behavior
- `headed` — Whether to use a visible browser
- `how_tos` — Any relevant how-to skill content for reference

## Process

1. **Launch browser** using Playwright MCP tools (headless unless `headed` is true)
2. **Navigate** to `{frontend_url}/operator`
3. **Log in** with the provided credentials via Entra ID login flow
4. **Verify login** — confirm you land on the operator page (Course Portfolio or Waitlist page)
   - If login fails, report CRITICAL BLOCKER immediately
5. **Execute role brief** — perform the actions described in the role brief, with realistic pacing:
   - Wait 3-10 seconds between actions (vary it naturally)
   - Read the page state before acting — don't click blindly
   - If a how-to skill exists for an action, follow its steps
   - If no how-to exists, navigate the UI by reading labels, buttons, and headings
6. **Log every action** with timestamp, what you did, and what happened
7. **Continue until** you run out of actions in the role brief, then cycle through them again with variations
8. **When finished** (coordinator signals or you've completed your scenarios), compile your action log

## Feature Area Behaviors

When performing actions, use the exact terminology from the UI. Reference how-to skills when available.

### waitlist
- Open the walk-up waitlist for today (see `how-tos:operator-open-waitlist` if available)
- Post tee time openings at various times (see `how-tos:operator-post-tee-time` if available)
- Monitor the waitlist for new golfer joins
- Close and reopen slots on the tee sheet
- Add golfers manually (see `how-tos:operator-add-golfer-manually` if available)

### Hints
If a `hint` is provided, adapt your behavior accordingly. For example:
- "post openings at 9:00 AM only" → post all tee time openings at 09:00
- "close slots while golfers are booking" → actively close tee time openings after posting them

## Output Format

Return this structure:

```markdown
## Operator Action Log

**Login:** {success | failed}
**Actions completed:** {N}
**Critical blockers:** {count or "none"}

### Actions
| Time | Action | Result | Notes |
|------|--------|--------|-------|
| {timestamp} | {what you did} | {success/failed/error} | {any details} |

### Issues Encountered
- {description of any UI issues, unexpected behavior, or errors — or "None"}

### Critical Blockers
- {if any — what happened and when}
```

## Guidelines

- Act like a real operator. Don't rush through actions — pace yourself naturally.
- Read the screen before clicking. If a button is disabled or missing, log it as an issue.
- If you encounter an error dialog or toast message, capture the text in your notes.
- Do NOT navigate outside `/operator/*` routes.
- Do NOT interact with dev tools, console, or browser internals.
- If the app becomes completely unresponsive (no response for 30+ seconds), report CRITICAL BLOCKER.
```

- [ ] **Step 2: Verify the file renders correctly**

Read the file back and confirm formatting.

- [ ] **Step 3: Commit**

```bash
git add .claude/agents/stress-test-operator.md
git commit -m "feat: add stress test operator agent definition"
```

---

### Task 4: Golfer Agent Definition

**Files:**
- Create: `.claude/agents/stress-test-golfer.md`

The golfer agent uses anonymous public flows and reads SMS via the API.

- [ ] **Step 1: Create the agent definition**

```markdown
---
name: stress-test-golfer
description: Golfer agent for stress tests. Uses anonymous public flows (join waitlist, accept openings) via Playwright browser and reads SMS via the dev API.
tools: Read, Bash, Grep, Glob
model: sonnet
---

# Stress Test Golfer

You are a golfer exercising the Shadowbrook public-facing UI during a stress test. You use anonymous flows (no login required) via a Playwright browser and read SMS messages via the dev API.

## Safety Rules

- **NEVER fix issues.** If something is broken, log it and move on. Do not attempt workarounds.
- **NEVER make changes to code.** No edits, no writes, no file modifications.
- **NEVER modify infrastructure, configuration, or deployment state.**
- **Report only.** Your job is to exercise the system and document what happens — nothing more.
- **Flag critical blockers immediately.** If you encounter something that halts testing — app crash, page completely unresponsive, environment down — report it as a CRITICAL BLOCKER in your output so the coordinator can halt the run.

## Inputs

You will receive:
- `frontend_url` — The base URL for the web app
- `api_url` — The base URL for the API (for dev SMS endpoints)
- `golfer_id` — Your golfer identity (assigned by coordinator). This is a string like "golfer-1", "golfer-2", etc. — you'll get an actual golfer ID from the system after joining.
- `feature_area` — Which feature to exercise (e.g., `waitlist`)
- `role_brief` — Specific behaviors to perform
- `hint` — Optional contention hint to shape behavior
- `headed` — Whether to use a visible browser

## Process

1. **Launch browser** using Playwright MCP tools (headless unless `headed` is true)
2. **Execute role brief** — perform the actions described, interacting with the public UI
3. **Read SMS messages** — after actions that trigger SMS (like joining a waitlist), poll the dev SMS API:
   ```bash
   curl -s {api_url}/dev/sms/ | jq '.[] | select(.direction == 0) | {to, body, timestamp}' | head -20
   ```
   Since you won't know your golfer ID until after joining, use the main SMS list endpoint and filter by your phone number or look for recent messages.
4. **Follow links in SMS** — when an SMS contains a URL (e.g., a tee time opening link), navigate to that URL in the browser
5. **Log every action** with timestamp, what you did, and what happened
6. **Continue until** you've completed your scenarios or the coordinator signals stop

## Feature Area Behaviors

### waitlist
- Navigate to `{frontend_url}/join` or `{frontend_url}/w/{shortCode}` to join the waitlist
  - You'll need the short code — the coordinator or hint will provide it, or look for it on the join page
  - Fill in your name and phone number (use a unique phone like `+1555000{golfer_number}`)
  - Submit the join form
- After joining, poll for SMS messages:
  ```bash
  curl -s {api_url}/dev/sms/ | jq '.[] | select(.to | contains("555000")) | {to, body, timestamp}'
  ```
- When you receive an SMS with a tee time opening link, navigate to that URL in the browser
- Accept or decline the opening (vary your behavior — accept most, decline occasionally)
- If multiple golfers are targeting the same slot (per hint), race to accept it

### Hints
If a `hint` is provided, adapt your behavior accordingly. For example:
- "all golfers target the 9:00 AM slot" → when you see a 9:00 AM opening, accept it immediately
- "golfers join and leave repeatedly" → join, leave, rejoin the waitlist multiple times

## Output Format

Return this structure:

```markdown
## Golfer {golfer_id} Action Log

**Actions completed:** {N}
**Critical blockers:** {count or "none"}

### Actions
| Time | Action | Result | Notes |
|------|--------|--------|-------|
| {timestamp} | Joined waitlist | success | Phone: +15550001 |
| {timestamp} | Received SMS: "Tee time available..." | — | |
| {timestamp} | Accepted 9:00 AM opening | conflict — already claimed | Expected contention |

### SMS Messages Received
| Time | Body |
|------|------|
| {timestamp} | {message text} |

### Contention Events
- {any races, conflicts, or unexpected behavior related to concurrent access}

### Issues Encountered
- {description of any UI issues, unexpected behavior, or errors — or "None"}

### Critical Blockers
- {if any — what happened and when}
```

## Guidelines

- Act like a real golfer. Read the page, fill in forms naturally, wait for responses.
- Use unique phone numbers so SMS messages don't collide between golfer agents.
- When you hit a conflict (e.g., "this tee time is no longer available"), log it as a contention event — this is expected and valuable data, not an error.
- Do NOT navigate to `/operator/*` or `/admin/*` routes.
- Do NOT attempt to log in. You are an anonymous user.
- If the app becomes completely unresponsive (no response for 30+ seconds), report CRITICAL BLOCKER.
- If you're waiting for an SMS and none arrives within 60 seconds, log it as a potential flow gap and move on.
```

- [ ] **Step 2: Verify the file renders correctly**

Read the file back and confirm formatting.

- [ ] **Step 3: Commit**

```bash
git add .claude/agents/stress-test-golfer.md
git commit -m "feat: add stress test golfer agent definition"
```

---

### Task 5: Stress Test Skill

**Files:**
- Create: `.claude/skills/stress-test/SKILL.md`

This is the main coordinator skill that parses args, launches agents, provides status updates, and assembles the report.

- [ ] **Step 1: Create the skill file**

```markdown
---
name: stress-test
description: Launch parallel AI agents (operator, golfers, observer) to exercise the system with realistic concurrent usage. Surfaces race conditions, eventual consistency issues, and concurrency bugs.
user-invocable: true
---

# Stress Test

Launch parallel AI agents to exercise the Shadowbrook system with realistic concurrent usage. An operator agent and N golfer agents interact with the UI via Playwright browsers while an observer agent monitors App Insights for errors and flow gaps.

## Usage

- `/stress-test waitlist` — test waitlist flows with 3 golfers against the test environment
- `/stress-test waitlist --golfers 5` — 5 concurrent golfers
- `/stress-test waitlist --headed` — visible browsers
- `/stress-test waitlist --env local` — hit localhost instead of test
- `/stress-test waitlist --hint "all golfers target 9:00 AM"` — shape contention
- `/stress-test waitlist --timeout 15` — 15-minute safety timeout

## Arguments

| Parameter | Default | Description |
|-----------|---------|-------------|
| `feature-area` | (required) | Feature to test: `waitlist` |
| `--golfers N` | 3 | Number of concurrent golfer agents |
| `--headed` | false | Open visible browsers instead of headless |
| `--env` | `test` | Target environment: `test` or `local` |
| `--timeout M` | 30 | Safety timeout in minutes |
| `--hint "..."` | none | Contention hint for agents |

## Environment Resolution

| Env | Frontend | API |
|-----|----------|-----|
| `test` | `https://white-stone-00610060f.1.azurestaticapps.net` | `https://shadowbrook-app-test.happypond-1a892999.eastus2.azurecontainerapps.io` |
| `local` | `http://localhost:3000` | `http://localhost:5221` |

App Insights (test env):
- App name: `shadowbrook-appinsights-test`
- Resource group: `shadowbrook-rg-test`

## Feature Area Role Briefs

### waitlist

**Operator:**
- Open the walk-up waitlist for today
- Post tee time openings at various times
- Monitor the waitlist for new golfer joins
- Close and reopen slots on the tee sheet
- Add golfers manually

**Golfer:**
- Join the walk-up waitlist (via `/join` or QR shortcode)
- Watch for tee time openings via SMS (dev SMS API)
- Accept or decline a tee time opening

## Process

### 1. Parse Arguments

Extract the feature area, golfer count, headed flag, environment, timeout, and hint from the invocation arguments.

### 2. Validate Prerequisites

- **Credentials:** Read `.local/test-credentials.md` and extract operator credentials. If the file is missing, stop and tell the user.
- **Environment reachability:** Verify the frontend URL is reachable:
  ```bash
  curl -s -o /dev/null -w "%{http_code}" {frontend_url}
  ```
  If not reachable, stop and tell the user.
- **Azure CLI (for observer):** Verify `az` is logged in:
  ```bash
  az account show --query name -o tsv
  ```
  If not logged in, tell the user to run `! az login`.
- **Feature area:** Verify the feature area has a role brief defined above. If not, stop and list available feature areas.

### 3. Load How-To Skills

Check `.claude/skills/how-tos/` for relevant how-to skills based on the feature area:

For `waitlist`:
```bash
ls .claude/skills/how-tos/operator-open-waitlist.md .claude/skills/how-tos/operator-post-tee-time.md .claude/skills/how-tos/operator-add-golfer-manually.md 2>/dev/null
```

Read any that exist — their content will be passed to agents for reference.

### 4. Announce the Run

Tell the user:
```
Starting stress test: {feature_area}
  Environment: {env} ({frontend_url})
  Agents: 1 operator + {N} golfers + 1 observer
  Hint: {hint or "none"}
  Timeout: {timeout} minutes
  Browser: {headed or headless}

Launching agents...
```

### 5. Launch Agents in Parallel

Use the Agent tool to dispatch all agents simultaneously. All agents run in the background so the coordinator can provide status updates.

**Observer agent** (background):
- `subagent_type: "stress-test-observer"`
- Prompt includes: App Insights app name, resource group, poll interval (30s), time offset (2m)

**Operator agent** (background):
- `subagent_type: "stress-test-operator"`
- Prompt includes: frontend URL, credentials (from `.local/test-credentials.md`), feature area, role brief, hint, headed flag, any how-to skill content

**Golfer agents** (background, one per golfer):
- `subagent_type: "stress-test-golfer"`
- Each gets a unique golfer_id: `golfer-1`, `golfer-2`, etc.
- Prompt includes: frontend URL, API URL, golfer_id, feature area, role brief, hint, headed flag

### 6. Monitor and Report

While agents are running:

- **Every ~2 minutes**, output a status update to the user:
  ```
  [Stress Test — {elapsed}] Agents active: operator, golfer-1, golfer-2, golfer-3, observer
  Recent activity:
    - Operator: opened waitlist, posted 9:00 AM opening
    - Golfer-1: joined waitlist
    - Golfer-2: joined waitlist
    - Observer: no errors (2 polls completed)
  ```

- **On circuit breaker:** If the observer returns with `CIRCUIT_BREAKER` in its output, immediately tell the user:
  ```
  ⚠ CIRCUIT BREAKER TRIGGERED — {reason}
  Shutting down remaining agents and compiling report...
  ```

- **On critical blocker from any agent:** If any agent returns with `CRITICAL BLOCKER` in its output:
  ```
  ⚠ CRITICAL BLOCKER from {agent}: {description}
  Shutting down remaining agents and compiling report...
  ```

- **On timeout:** If the safety timeout is reached:
  ```
  ⚠ Safety timeout reached ({timeout} minutes). Compiling report from available results...
  ```

### 7. Assemble Report

When all agents have returned (or after circuit breaker/timeout), compile the final report:

1. Collect the output from each agent
2. Write the report to `docs/qa/stress-test-{YYYY-MM-DD}-{feature}.md` using the format below
3. Tell the user: "Stress test complete. Report saved to `docs/qa/stress-test-{date}-{feature}.md`"
4. Print a brief summary inline (don't make them open the file to see results)

### Report Format

```markdown
# Stress Test Report: {feature}

**Date:** {date}
**Duration:** {elapsed}
**Environment:** {env} ({frontend_url})
**Agents:** 1 operator, {N} golfers, 1 observer
**Hint:** {hint or "none"}
**Exit reason:** completed | circuit-breaker | timeout | critical-blocker

## Operator Actions

| Time | Action | Result | Notes |
|------|--------|--------|-------|

## Golfer Results

| Golfer | Time | Action | Result | Notes |
|--------|------|--------|--------|-------|

## Contention Events

Races, conflicts, and eventual consistency observations from all agents.

## Observer Findings

### Errors
{from observer report}

### Warnings
{from observer report}

### Concurrency Events
{from observer report}

### Flow Gaps
{from observer report}

## Summary

{AI-generated summary covering:
- How many actions completed successfully vs. failed
- Contention handling quality (did conflicts resolve cleanly?)
- Any unhandled errors or exceptions
- Eventual consistency observations (data appeared stale, events delayed, etc.)
- Recommendations for follow-up}
```

## Notes

- The observer has a 1-2 minute ingestion delay from App Insights. Early actions may not show up in the first poll.
- Golfer agents use unique phone numbers (`+1555000{N}`) so SMS messages don't collide.
- If running against `local`, make sure `make dev` is running first.
- The observer requires an active `az login` session. If it expires mid-run, the observer will report a CRITICAL BLOCKER.
```

- [ ] **Step 2: Verify the file renders correctly**

Read the file back and confirm all sections are complete, arguments match the spec, environment URLs are correct, and the process flow is coherent.

- [ ] **Step 3: Commit**

```bash
git add .claude/skills/stress-test/SKILL.md
git commit -m "feat: add stress test coordinator skill"
```

---

### Task 6: Verify App Insights Resource Names

**Files:**
- Read: `infra/bicep/modules/app-insights.bicep`
- Read: `infra/bicep/parameters/test.bicepparam` or similar parameter files

The skill and observer reference `shadowbrook-appinsights-test` and `shadowbrook-rg-test` — verify these are the actual resource names.

- [ ] **Step 1: Check the Bicep parameter files for test environment resource names**

```bash
grep -r "appinsights\|app-insights\|appInsights" infra/ --include="*.bicep" --include="*.bicepparam" -l
```

Read the relevant files and find the actual App Insights resource name and resource group for the test environment.

- [ ] **Step 2: Update skill and observer if names differ**

If the actual names are different from `shadowbrook-appinsights-test` / `shadowbrook-rg-test`, update:
- `.claude/skills/app-insights-queries/SKILL.md` — the Prerequisites section
- `.claude/skills/stress-test/SKILL.md` — the App Insights section under Environment Resolution
- `.claude/agents/stress-test-observer.md` — any hardcoded references (there shouldn't be — these are passed as inputs)

- [ ] **Step 3: Commit if changes were made**

```bash
git add -A
git commit -m "fix: correct App Insights resource names in stress test skill"
```

---

### Task 7: End-to-End Dry Run

Verify the full skill structure is correct and all pieces connect.

- [ ] **Step 1: Verify all files exist**

```bash
ls -la .claude/skills/stress-test/SKILL.md
ls -la .claude/skills/app-insights-queries/SKILL.md
ls -la .claude/agents/stress-test-operator.md
ls -la .claude/agents/stress-test-golfer.md
ls -la .claude/agents/stress-test-observer.md
```

All five files should exist.

- [ ] **Step 2: Verify skill frontmatter**

Read each file and confirm:
- Skills have `name`, `description`, and `user-invocable` (where appropriate)
- Agents have `name`, `description`, `tools`, and `model`
- No frontmatter syntax errors (proper `---` delimiters)

- [ ] **Step 3: Cross-reference consistency**

Verify:
- The stress-test skill references agent names that match the agent definition `name` fields: `stress-test-operator`, `stress-test-golfer`, `stress-test-observer`
- The observer agent references the KQL skill path: `.claude/skills/app-insights-queries/SKILL.md`
- The operator agent references how-to skill names that exist: `how-tos:operator-open-waitlist`, `how-tos:operator-post-tee-time`, `how-tos:operator-add-golfer-manually`
- Environment URLs in the skill match the spec
- Report output path matches the spec: `docs/qa/stress-test-{YYYY-MM-DD}-{feature}.md`

- [ ] **Step 4: Commit the plan itself**

```bash
git add docs/superpowers/plans/2026-04-03-stress-test-skill.md
git commit -m "docs: add stress test skill implementation plan"
```
