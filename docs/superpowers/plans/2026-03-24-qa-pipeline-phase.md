# QA Pipeline Phase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a QA phase to the agentic pipeline so merged stories are validated against acceptance criteria before closing.

**Architecture:** New `QA` project board status, a `/qa` skill that orchestrates local QA sessions, a `qa-tester` agent that verifies acceptance criteria via headed Playwright, and sprint manager updates to route merged issues to QA instead of Done.

**Tech Stack:** GitHub Projects API, Claude Code skills/agents, Playwright MCP

**Spec:** `docs/superpowers/specs/2026-03-24-qa-pipeline-phase-design.md`

---

## File Map

| Action | Path | Responsibility |
|--------|------|---------------|
| Create | `.claude/agents/qa-tester.md` | QA agent role definition |
| Create | `.claude/skills/qa/SKILL.md` | `/qa` skill — orchestrates QA sessions |
| Modify | `.claude/agents/sprint-manager.md` | Post-merge → QA, `Relates to` instead of `Closes` |
| Modify | `.claude/skills/agent-pipeline/SKILL.md` | Add QA role icon, QA status to tables |
| Modify | `.claude/CLAUDE.md` | Add QA status option ID (after manual creation) |

---

### Task 1: Create the QA Tester Agent Definition

**Files:**
- Create: `.claude/agents/qa-tester.md`

- [ ] **Step 1: Create the agent file**

```markdown
---
name: qa-tester
description: QA tester for verifying user story acceptance criteria against a running application via headed Playwright browser.
tools: Read, Bash, Grep, Glob
model: sonnet
---

# QA Tester

You verify acceptance criteria for user stories against a running application using a headed browser via Playwright MCP.

## Role

You are a QA tester. You receive a user story with acceptance criteria and a deployed environment URL. You systematically walk through each acceptance criterion in a browser, verify it works as specified, and produce a structured report.

You do NOT create issues, change statuses, or interact with GitHub. You only produce the report.

## Process

1. Review the user story and list all acceptance criteria
2. For each AC, in order:
   a. Navigate to the relevant page in the application
   b. Perform the described user interaction
   c. Verify the expected outcome
   d. Take a screenshot (pass or fail)
   e. If fail: note what actually happened vs. what was expected
3. Note any incidental issues encountered during the walkthrough (broken links, console errors, missing loading states) — but do not explore beyond the story scope
4. Produce the structured report

## Output Format

Return this exact markdown structure:

~~~markdown
## QA Report — #{issue_number}: {story_title}

**Environment:** {url}
**Date:** {date}
**Result:** {PASS | FAIL}

### Acceptance Criteria Results

#### AC 1: {criterion text}
- **Result:** PASS | FAIL
- **Screenshot:** {path}
- **Notes:** {what happened — required for FAIL, optional for PASS}

#### AC 2: {criterion text}
...

### Incidental Issues
- {description of any issues noticed during testing, or "None"}

### Suggested Actions
#### Bugs
- {title} — {one-line description of failure}

#### Potential Stories
- {title} — {one-line description of gap discovered}
~~~

## Guidelines

- Be methodical. Test exactly what the AC says, not what you think it should say.
- Take screenshots at the moment of verification — the screenshot should show the pass or fail state.
- If you cannot test an AC because a prerequisite is missing (e.g., no test data), report it as FAIL with a note explaining why.
- If the application is down or unreachable, report all ACs as FAIL with a note.
- Keep notes concise. "Expected: redirect to /dashboard. Actual: stayed on /login with no error message." is better than a paragraph.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/agents/qa-tester.md
git commit -m "feat: add qa-tester agent definition"
```

---

### Task 2: Create the `/qa` Skill

**Files:**
- Create: `.claude/skills/qa/SKILL.md`

- [ ] **Step 1: Create the skill directory and file**

```markdown
---
name: qa
description: Run QA validation against deployed environment for user stories. Verifies acceptance criteria via headed Playwright browser, posts reports, files bugs.
user-invocable: true
---

# QA Validation

Validate user stories against a deployed environment by walking through acceptance criteria in a headed browser.

## Usage

- `/qa 247` — test a specific issue
- `/qa` — test all issues in QA status in the current iteration

## Single Issue Mode

When an issue number is provided:

1. **Fetch the issue:**
   ```bash
   gh issue view {number} --repo benjamingolfco/shadowbrook
   ```

2. **Find the Issue Plan comment** — look for the pinned comment containing `## Issue Plan`. Extract:
   - The `### Story` section (user story and acceptance criteria)
   - The issue title

3. **Determine the environment URL:**
   - Default: `https://dev.shadowbrook.golf` (or override via argument: `/qa 247 --url http://localhost:3000`)
   - If the URL is not reachable, stop and tell the user

4. **Dispatch the QA tester agent** via the Agent tool:
   - `subagent_type: "qa-tester"`
   - Include in the prompt:
     - The issue number and title
     - The full story and acceptance criteria text
     - The environment URL
     - Instruction to use Playwright MCP tools in headed mode
   - Wait for the agent to return the QA report

5. **Post the report on the issue:**
   ```bash
   gh issue comment {number} --repo benjamingolfco/shadowbrook --body "{report}"
   ```
   Prefix the comment with the QA role icon:
   ```markdown
   ### 🔍 QA Tester — Validation Report for #{number}

   {report content}
   ```

6. **Handle results:**

   **All ACs pass:**
   - Check for open sub-issues with the `qa-bug` label:
     ```bash
     gh api repos/benjamingolfco/shadowbrook/issues/{number}/sub_issues --jq '[.[] | select(.state == "open") | select(.labels[].name == "qa-bug")] | length'
     ```
   - If open qa-bugs exist: tell the user "All ACs pass but {N} qa-bug(s) are still open. Leaving in QA."
   - If no open qa-bugs: move issue to Done and close it
     ```bash
     # Move to Done status (update option ID after creating QA status)
     gh project item-edit --project-id {id} --id {item_id} --field-id PVTSSF_lADOD3a3vs4BOVqOzg9EexU --single-select-option-id b9a85561
     gh issue close {number} --repo benjamingolfco/shadowbrook
     ```
   - Tell the user the result

   **Failures found:**
   - For each failure, offer to create a bug issue:
     ```bash
     gh api repos/benjamingolfco/shadowbrook/issues -X POST \
       -f title="{suggested bug title}" \
       -f body="Filed from QA validation of #{number}.\n\n**Expected:** {expected}\n**Actual:** {actual}\n\n**Screenshot:** {path}" \
       -f type="Bug"
     ```
   - Add the `qa-bug` label to each created bug:
     ```bash
     gh issue edit {bug_number} --add-label "qa-bug" --repo benjamingolfco/shadowbrook
     ```
   - Link each bug as a sub-issue of the parent story:
     ```bash
     gh api repos/benjamingolfco/shadowbrook/issues/{parent}/sub_issues -X POST -F sub_issue_id={bug_id}
     ```
   - Leave the parent issue in QA status
   - Tell the user: "{N} bug(s) filed and linked to #{number}. Issue stays in QA."

   **Gaps discovered:**
   - Present the suggested story titles to the user
   - Ask if they want to create them as new issues (these are NOT sub-issues — they are new scope)

## Queue Mode

When no issue number is provided:

1. **Query the project board** for issues in QA status in the current iteration:
   ```bash
   gh api graphql -f query='
     query {
       organization(login: "benjamingolfco") {
         projectV2(number: 1) {
           items(first: 100, query: "iteration:@current") {
             nodes {
               fieldValueByName(name: "Status") {
                 ... on ProjectV2ItemFieldSingleSelectValue { name }
               }
               content {
                 ... on Issue { number title state }
               }
             }
           }
         }
       }
     }
   '
   ```
   Filter for items where status is "QA" and state is "OPEN".

2. **Present the list:**
   "Found {N} issues in QA: #{a}, #{b}, #{c}. Starting QA session."

   If no issues found: "No issues in QA status in the current iteration."

3. **Run single-issue mode for each** — sequentially, not in parallel.

4. **After all issues, print session summary:**
   "{N} issues tested. {passed} passed and moved to Done. {failed} had bugs filed."

## Re-testing

When a story is re-tested (bugs were previously filed):
- The agent runs the full AC suite again (not just previously-failed criteria)
- New bugs may be filed if regressions are found
- The story moves to Done only when all ACs pass AND no open `qa-bug` sub-issues remain

## QA Bug Lifecycle

Bugs created by this skill:
- Are labeled `qa-bug`
- Are linked as sub-issues to the parent story
- Skip BA story refinement — the QA report provides the reproduction steps and screenshots
- Should be set to Ready status immediately (they are implementation-ready)
- Go through normal implementation pipeline from Ready onward
```

- [ ] **Step 2: Commit**

```bash
git add .claude/skills/qa/SKILL.md
git commit -m "feat: add /qa skill for QA validation sessions"
```

---

### Task 3: Update Sprint Manager — Post-Merge Routing

**Files:**
- Modify: `.claude/agents/sprint-manager.md:137` (PR body `Closes` → `Relates to`)
- Modify: `.claude/agents/sprint-manager.md:144` (merge cascade status Done → QA)
- Modify: `.claude/agents/sprint-manager.md:170` (merge cascade sets Done → QA)

- [ ] **Step 1: Change PR body format**

In `.claude/agents/sprint-manager.md`, find Step 4 (line ~137):
```
Body: cover all agents' contributions with a test plan. Include "Closes #{number}"
```
Change to:
```
Body: cover all agents' contributions with a test plan. Include "Relates to #{number}"
```

- [ ] **Step 2: Change merge cascade status**

In the Merge Cascade section (line ~170), find:
```
2. **Set the issue status to Done.**
```
Change to:
```
2. **Set the issue status to QA.** (The issue moves to Done only after QA validation passes via the `/qa` skill.)
```

- [ ] **Step 3: Update Step 5 CI+review pass outcome**

In Step 5 (line ~144), find:
```
**CI passes + review approved:** Set status to **Done**.
```
Change to:
```
**CI passes + review approved:** Set status to **QA**.
```

- [ ] **Step 4: Update the routing summary table**

In the Routing Summary table (line ~421), find:
```
| Implementing | CI + review pass | Merge to sprint branch, set Done, trigger merge cascade (Sprint) |
```
Change to:
```
| Implementing | CI + review pass | Merge to sprint branch, set QA, trigger merge cascade (Sprint) |
```

- [ ] **Step 5: Update merge cascade blocker check**

In the Merge Cascade section (line ~172), find:
```
4. **For each blocked sprint issue:** check if ALL of its blockers are now Done.
```
Change to:
```
4. **For each blocked sprint issue:** check if ALL of its blockers are now QA or Done.
```

This is critical — without this change, dependent issues would never unblock because merged issues are now in QA, not Done.

- [ ] **Step 6: Update Sprint Completion logic**

In Sprint Completion section (line ~180), find:
```
When all sprint issues are Done:
```
Change to:
```
When all sprint issues are Done or QA:
```
Note: Sprint completion triggers when all implementation work is merged. QA happens locally and doesn't block the sprint PR. The owner runs `/qa` and resolves issues before or after merging the sprint PR to main.

- [ ] **Step 7: Commit**

```bash
git add .claude/agents/sprint-manager.md
git commit -m "feat: route merged issues to QA instead of Done"
```

---

### Task 4: Update Agent Pipeline Skill

**Files:**
- Modify: `.claude/skills/agent-pipeline/SKILL.md`

- [ ] **Step 1: Add QA role icon**

In the Role Icons table (line ~200), add:
```
| 🔍 | QA Tester |
```

- [ ] **Step 2: Add QA to the Project Statuses table**

In the Project Statuses table (line ~85), insert between the `Implementing` row and the `Done` row:
```
| QA | Merged — awaiting acceptance criteria validation via `/qa` skill | Implementation |
```

- [ ] **Step 3: Update the Issue Plan Section Lifecycle table**

In the Section Lifecycle table (line ~346), change the Done row and add QA:
```
| QA | Update Phase to QA. History entry for merge. |
| Done | Update Phase to Done after QA passes. Final History entry. |
```

- [ ] **Step 4: Update the Sprint Execution Flow**

In the Sprint Execution Flow (line ~380), the last line currently reads:
```
  → monitors PR lifecycle (CI, review) — re-dispatches agents as needed
```
Add after it:
```
  → on merge: sets status to QA (owner validates via /qa skill)
```

- [ ] **Step 5: Update the Merge Cascade Flow**

In the Merge Cascade Flow (line ~401), find:
```
  → sets linked issue status to Done
```
Change to:
```
  → sets linked issue status to QA
```

And find:
```
  → for each blocked sprint issue: check if ALL blockers now Done
```
Change to:
```
  → for each blocked sprint issue: check if ALL blockers now QA or Done
```

- [ ] **Step 6: Update the Routing Summary in SKILL.md**

In the Routing Summary table (line ~421), find:
```
| Implementing | CI + review pass | Merge to sprint branch, set Done, trigger merge cascade (Sprint) |
```
Change to:
```
| Implementing | CI + review pass | Merge to sprint branch, set QA, trigger merge cascade (Sprint) |
```

- [ ] **Step 7: Commit**

```bash
git add .claude/skills/agent-pipeline/SKILL.md
git commit -m "feat: add QA status and role icon to pipeline protocol"
```

---

### Task 5: Update CLAUDE.md with QA Status

**Files:**
- Modify: `.claude/CLAUDE.md`

**Note:** This task requires the `QA` status to be created manually in GitHub Project settings first. The option ID is not known until then.

- [ ] **Step 1: Add QA status to the status table**

In the Status field table in CLAUDE.md, add between `Ready to Merge` and `Done`:
```
| QA | `{option_id}` |
```

- [ ] **Step 2: Update the pipeline statuses summary**

In CLAUDE.md, find the pipeline statuses summary line (line ~187):
```
- **Pipeline statuses:** (no status) → Needs Story → **Ready** → Implementing → Done
```
Change to:
```
- **Pipeline statuses:** (no status) → Needs Story → **Ready** → Implementing → QA → Done
```

- [ ] **Step 3: Add the `qa-bug` label documentation**

In the Issue Labels table, add:
```
| `qa-bug` | Bug filed from QA validation — skip BA refinement, goes straight to Ready |
```

- [ ] **Step 4: Commit**

```bash
git add .claude/CLAUDE.md
git commit -m "docs: add QA status ID and qa-bug label to project reference"
```

---

### Task 6: Create the `qa-bug` Label on GitHub

- [ ] **Step 1: Create the label**

```bash
gh label create "qa-bug" --description "Bug filed from QA validation" --color "d73a4a" --repo benjamingolfco/shadowbrook
```

- [ ] **Step 2: Verify**

```bash
gh label list --repo benjamingolfco/shadowbrook | grep qa-bug
```

---

### Task 7: Manual Step — Create QA Status on Project Board

This cannot be automated via API. The owner must:

1. Go to the GitHub Project settings (benjamingolfco project #1)
2. Edit the Status field
3. Add a new option: `QA`
4. Position it between `Ready to Merge` and `Done`
5. Copy the option ID
6. Update CLAUDE.md with the option ID (Task 5, Step 1)

---

### Task 8: Verify the Pipeline Docs Are Consistent

- [ ] **Step 1: Read all modified files and check for references to the old "Done" post-merge behavior**

Search for any remaining references that say issues go to Done after merge:
```bash
# Search agent and skill files for "Done" in merge context
grep -rn "Done" .claude/agents/sprint-manager.md .claude/skills/agent-pipeline/SKILL.md | grep -i "merge\|after.*PR\|status.*Done"
```

Fix any inconsistencies found.

- [ ] **Step 2: Verify the `/qa` skill is listed in settings**

Check that the qa skill directory will be picked up by Claude Code's skill discovery. Skills in `.claude/skills/*/SKILL.md` are auto-discovered.

```bash
ls -la .claude/skills/qa/SKILL.md
```

- [ ] **Step 3: Final commit if any fixes needed**

```bash
git add -A
git commit -m "fix: resolve remaining Done references in pipeline docs"
```
