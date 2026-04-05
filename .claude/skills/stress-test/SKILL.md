---
name: stress-test
description: Launch parallel AI agents (operator, golfers, observer) to exercise the system with realistic concurrent usage. Surfaces race conditions, eventual consistency issues, and concurrency bugs.
user-invocable: true
---

# Stress Test

Orchestrate parallel AI agents (operator, golfers, observer) against a deployed environment to surface race conditions, eventual consistency issues, and concurrency bugs under realistic concurrent load.

## Usage

```
/stress-test waitlist
/stress-test waitlist --golfers 5
/stress-test waitlist --headed
/stress-test waitlist --env local
/stress-test waitlist --hint "all golfers target 9:00 AM"
/stress-test waitlist --timeout 15
```

## Arguments

| Parameter | Default | Description |
|-----------|---------|-------------|
| feature-area | (required) | Feature to test: `waitlist` |
| --golfers N | 3 | Number of concurrent golfer agents |
| --headed | false | Open visible browsers instead of headless |
| --env | test | Target environment: `test` or `local` |
| --timeout M | 30 | Safety timeout in minutes |
| --hint "..." | none | Contention hint passed to all agents to encourage focused concurrent activity |

## Environment Resolution

| Env | Frontend | API |
|-----|----------|-----|
| test | https://white-stone-00610060f.1.azurestaticapps.net | https://teeforce-app-test.happypond-1a892999.eastus2.azurecontainerapps.io |
| local | http://localhost:3000 | http://localhost:5221 |

**App Insights (test env):** app name `teeforce-insights-test`, resource group `teeforce-test-rg`

## Feature Area Role Briefs

### waitlist

**Operator role:**
- Open the walk-up waitlist for today
- Post tee time openings at various times throughout the session
- Monitor the waitlist for new golfer joins
- Close and reopen slots on the tee sheet to create contention
- Add golfers manually when the waitlist has entries

**Golfer role:**
- Join the walk-up waitlist via `/join` or QR shortcode
- Watch for tee time openings (simulated via dev SMS API)
- Accept or decline a tee time opening when notified

## Process

### Step 1: Parse Arguments

Extract from the invocation:
- `feature-area` — required; must be a known area (currently: `waitlist`)
- `--golfers N` — number of golfer agents, default 3
- `--headed` — boolean flag, default false (headless)
- `--env` — `test` or `local`, default `test`
- `--timeout M` — minutes before forced shutdown, default 30
- `--hint "..."` — optional contention hint string

### Step 2: Validate Prerequisites

Run all checks before launching agents. Stop on any failure.

**Credentials:**
Read `.local/test-credentials.md` from the repo root. Extract operator credentials (username and password). If the file does not exist or operator credentials are missing, stop and tell the user:
> "Missing operator credentials. Add them to `.local/test-credentials.md` before running a stress test."

**Environment reachability:**
```bash
curl -s -o /dev/null -w "%{http_code}" {frontend_url}
```
If the response is not 200 (or 301/302 for redirects), stop:
> "Environment unreachable: {frontend_url} returned {status}. Check that the environment is up."

If `--env local` is specified, remind the user that `make dev` must be running.

**Azure CLI session (required for observer):**
```bash
az account show --query name -o tsv
```
If not logged in (non-zero exit or error output), stop:
> "Not logged in to Azure CLI. Run `az login` before starting a stress test. The observer agent requires an active session."

**Feature area:**
Verify the given feature area has a role brief in this skill. If not recognized, stop:
> "Unknown feature area: {area}. Available areas: waitlist"

### Step 3: Announce the Run

Before launching, output a clear summary to the user:

```
Starting stress test: {feature-area}
  Environment:  {env} ({frontend_url})
  Agents:       1 operator + {N} golfers + 1 observer
  Browser:      {headed ? "headed (visible)" : "headless"}
  Timeout:      {M} minutes
  Hint:         {hint or "none"}

Launching agents in parallel...
```

### Step 4: Launch Operator for Setup

The operator must open the waitlist first so golfers have a short code to join with. Launch the operator agent in the **foreground** with a setup-only instruction.

**Operator setup agent:**
- `subagent_type`: `stress-test-operator` (fallback: `general-purpose` with agent definition from `.claude/agents/stress-test-operator.md` embedded in prompt)
- Prompt includes:
  - Frontend URL: `{frontend_url}`
  - Operator credentials (username, password from `.local/test-credentials.md`)
  - Headed flag: `{headed}`
  - Instruction: Log in, open the walk-up waitlist for today, and report back the **4-digit short code**. Then stop — you will be re-launched for the full session after golfers are set up.
  - Load your own how-to skills as defined in your agent definition.

Wait for the operator to return. Extract the **short code** from the response. If the operator reports a CRITICAL BLOCKER (login failed, environment down), stop the run.

Tell the user: `Waitlist opened. Short code: {code}. Launching golfers and observer...`

### Step 5: Launch Remaining Agents in Parallel

Now launch the operator (full session), observer, and all golfers simultaneously in the background.

**Note on `subagent_type`:** Custom agent types (`stress-test-operator`, `stress-test-golfer`, `stress-test-observer`) require a session restart after the agent files are first created. If a custom type is not available, use `general-purpose` and read the agent definition file (e.g., `.claude/agents/stress-test-operator.md`) to embed its full content in the prompt.

**Observer agent (background):**
- `subagent_type`: `stress-test-observer` (fallback: `general-purpose`)
- Prompt includes:
  - App Insights app name: `teeforce-insights-test`
  - App Insights resource group: `teeforce-test-rg`
  - API base URL: `{api_url}`
  - Feature area being tested: `{feature-area}`
  - Session duration hint: `{timeout} minutes`
  - Instruction: monitor for errors, warnings, concurrency events, and flow gaps; emit `CIRCUIT_BREAKER` if critical system degradation is detected

**Operator agent (background, full session):**
- `subagent_type`: `stress-test-operator` (fallback: `general-purpose`)
- Prompt includes:
  - Frontend URL: `{frontend_url}`
  - Operator credentials (username, password from `.local/test-credentials.md`)
  - Role brief: the Operator role description from the feature area section above
  - Hint: `{hint}` (if provided)
  - Headed flag: `{headed}`
  - Instruction: The waitlist is already open with short code `{code}`. Perform operator actions continuously: post tee time openings, monitor joins, cancel/repost slots, add golfers manually. Log each action with a timestamp. Load your own how-to skills.

**Golfer agents (background, one per golfer, N total):**

For each golfer index 1..N:
- `subagent_type`: `stress-test-golfer` (fallback: `general-purpose`)
- Prompt includes:
  - `golfer_id`: the index (1, 2, 3, ...)
  - `short_code`: `{code}` (from the operator setup step)
  - Phone number: `555555000{golfer_id}` (10-digit format, e.g. `5555550001` for golfer 1)
  - Frontend URL: `{frontend_url}`
  - API base URL: `{api_url}`
  - Role brief: the Golfer role description from the feature area section above
  - Hint: `{hint}` (if provided)
  - Headed flag: `{headed}`
  - Instruction: join the waitlist using short code `{code}` and phone number `555555000{golfer_id}`; watch for SMS; accept/decline openings; log each action with a timestamp

### Step 6: Monitor and Report

While agents are running:

**Every ~2 minutes**, output a status pulse:
```
[{elapsed}m] Status: {N} agents active
  Operator: {last known action or "running"}
  Golfers:  {completed}/{total} responded
  Observer: monitoring
```

**On CIRCUIT_BREAKER signal from observer:**
```
CIRCUIT BREAKER TRIGGERED — observer detected critical system degradation.
Stopping all agents and compiling available results.
```
Collect whatever partial results exist from agents that have returned and proceed to Step 7.

**On critical blocker from any agent** (agent returns with a message indicating a hard blocker, e.g. login failure, environment down):
```
Critical blocker from {agent}: {message}
Stopping run. Fix the blocker and retry.
```

**On timeout:**
```
Timeout reached ({M} minutes). Compiling results from agents that have returned.
```
Collect partial results and proceed to Step 7.

### Step 7: Assemble Report

Collect output from all agents. Write the report to:
```
docs/qa/stress-test-{YYYY-MM-DD}-{feature-area}.md
```
Use today's date. If a file at that path already exists, append a suffix: `-2`, `-3`, etc.

**Report format:**

```markdown
# Stress Test Report — {feature-area} — {YYYY-MM-DD}

**Duration:** {elapsed} minutes  
**Environment:** {env} ({frontend_url})  
**Agents:** 1 operator + {N} golfers + 1 observer  
**Hint:** {hint or "none"}  
**Exit reason:** {Completed | Circuit breaker | Timeout | Blocker — description}

---

## Operator Actions

| Time | Action | Result |
|------|--------|--------|
| ... | ... | ... |

## Golfer Results

| Golfer | Phone | Joined Waitlist | Received Offer | Accepted | Notes |
|--------|-------|-----------------|----------------|----------|-------|
| 1 | +15550001 | ... | ... | ... | ... |

## Contention Events

Instances where multiple agents acted on the same resource simultaneously:

- {timestamp}: {description}

## Observer Findings

### Errors
- {timestamp}: {error message} ({count} occurrences)

### Warnings
- {timestamp}: {warning message}

### Concurrency Events
- {timestamp}: {description of concurrent or race-condition-adjacent event}

### Flow Gaps
- {description of expected event or state that did not appear in logs}

---

## Summary

{2–4 sentence narrative of what happened, what contention was observed, and whether the system handled it correctly.}

### Issues Found

{Bulleted list of bugs or anomalies worth investigating. "None observed." if clean.}
```

After writing the file, tell the user:
> "Stress test complete. Report written to `docs/qa/stress-test-{date}-{feature}.md`."

---

## Notes

- App Insights has a 1–2 minute ingestion delay. Observer results near the end of the run may be incomplete.
- Each golfer agent uses a unique phone number (`+1555000{N}`) to distinguish SMS flows in logs and the dev SMS API.
- If running against `local`, `make dev` must be running before invoking this skill.
- The observer agent requires an active `az login` session. If the session expires mid-run, observer will return incomplete results without triggering a circuit breaker.
- The operator and golfer agents require Playwright MCP tools to be available.
- This skill is designed to find bugs under concurrency — a clean run (no issues found) is still a valid and useful result.
