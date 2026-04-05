# QA How-Tos and Session Screenshots

## Overview

Extend the `local-qa-cycle` skill so the QA agent builds up a library of reusable how-to skills as it walks flows, and captures screenshots as session evidence for both successful actions and bugs.

## How-To Skills

### Location

`.claude/skills/how-tos/` — flat folder, one markdown file per flow.

### Naming

`{verb}-a-{noun}.md` — e.g., `create-a-golfer.md`, `post-a-tee-time.md`, `book-a-tee-time.md`.

### File Structure

```markdown
---
name: how-tos:{flow-name}
description: Use when you need to {do X} in the Teeforce app running locally
---

# {Flow Name}

## Prerequisites
- **Required data:** {org must exist, course must exist, etc.}
- **Required role/page:** {must be logged in as admin, start from /admin/courses}
- **Depends on:** {link to other how-to if this flow requires a prior flow}

## Steps
1. Navigate to {url}
2. Click {element}
3. Fill in {field} with {example valid value}
4. ...
5. Verify: {what success looks like — toast, redirect, data appears}

## Notes
{Any gotchas, e.g. "slug is auto-generated from name"}
```

### Creation and Update Rules

- **Create:** After the QA agent successfully completes a flow and no how-to exists for that flow.
- **Update:** After the QA agent successfully completes a flow and the existing how-to's steps differ from what was observed (new fields, different URLs, changed flow).
- **Timing:** Inline, immediately after completing the flow — while steps are fresh in context.

### How-Tos as Input

Before exploring, the QA agent reads existing how-tos from `.claude/skills/how-tos/` to know what flows exist and what steps are expected. This provides a baseline — if something changed, the agent updates the how-to.

## Session Screenshots

### Location

`docs/qa/screenshots/{YYYY-MM-DD}/` — one folder per QA session date.

### Naming

Descriptive names tied to the flow and moment: `{flow}-{moment}.png`

Examples:
- `create-a-golfer-form.png`
- `create-a-golfer-success.png`
- `book-a-tee-time-error-no-slots.png`

### When to Capture

- **Successful flows:** Initial page, filled form state, success confirmation.
- **Bugs:** The broken state, error messages, console errors — any visual evidence of the issue.

### Integration with Session Logs

The existing session log (`docs/qa/local-qa-{date}.md`) references screenshot paths in its issue reports and happy-path verifications.

## Changes to local-qa-cycle Skill

All changes are in **Step 2 (QA — Walk Happy Paths)** — additions to the QA agent's prompt. No new workflow steps are needed.

### Additions to QA Agent Prompt

1. **Read existing how-tos** before exploring: glob `.claude/skills/how-tos/*.md` and use them as a baseline for known flows and expected steps.
2. **Create/update how-tos** inline after each successfully completed flow.
3. **Take screenshots** at notable moments (page loads, filled forms, confirmations, bugs) and save to `docs/qa/screenshots/{date}/`.
4. **Reference screenshots** in the session log output.
