# QA Pipeline Phase Design

## Problem

The agentic pipeline currently moves issues straight to Done after merge. There is no verification step to confirm that the implemented feature actually works as specified in the user story, or to catch bugs and missing requirements that slipped through unit tests, integration tests, and code review.

## Solution

Add a QA phase to the pipeline that validates each user story's acceptance criteria against the deployed environment after merge. QA is performed locally via a `/qa` skill that dispatches a QA agent with headed Playwright browser automation. The owner watches the agent test in real-time and reviews a structured report.

## Design

### 1. New Pipeline Status: QA

A new `QA` status on the GitHub Project board, positioned between Ready to Merge and Done.

**Updated pipeline flow:**
```
Ready -> Implementing -> CI Pending -> In Review -> Changes Requested -> Ready to Merge -> QA -> Done
```

After a PR merges, the sprint manager moves the issue to QA instead of Done. The issue stays in QA until all acceptance criteria pass with no open bugs.

**Merge cascade is unchanged** — dependent issues unblock on PR merge, not on QA completion. QA validates the story; it does not gate downstream work.

**Setup:** The QA status must be created manually in GitHub Project settings. Once created, add the option ID to CLAUDE.md with the other status IDs.

### 2. Sprint Manager Changes

Three modifications to `.claude/agents/sprint-manager.md`:

1. **PR body format:** Use `Relates to #N` instead of `Closes #N` so merging the PR does not auto-close the issue. Issues are closed when they move to Done after QA.
2. **Post-merge status:** Move issues to `QA` instead of `Done` after merge.
3. **Merge cascade:** No changes — still triggers on merge, unblocks dependent issues as before.

### 3. QA Agent Definition

A new file `.claude/agents/qa-tester.md` defining the QA tester role.

**Role:** Verify acceptance criteria against a running application using a headed browser.

**Instructions:**
- Work through acceptance criteria sequentially, one at a time
- For each AC: navigate to the relevant page, perform the described interaction, verify the expected outcome
- Take a screenshot after each AC verification (pass or fail)
- On failure: note what actually happened vs. what was expected, include the screenshot
- Note any incidental issues encountered during the walkthrough (broken links, console errors, missing loading states) but do not explore beyond the story scope
- Do not create issues, change statuses, or interact with GitHub — only produce the report

**Output format:** Structured markdown report containing:
- Issue number and story title
- Pass/fail per acceptance criterion with screenshot paths
- Description of failures (actual vs. expected behavior)
- Incidental issues encountered
- Suggested bug titles for failures
- Suggested story titles for gaps discovered

The agent receives all context (story, ACs, environment URL) from the skill — it has no inputs of its own.

### 4. `/qa` Skill

A user-invocable skill with two modes:

**Single issue: `/qa 247`**
1. Fetch the issue via `gh issue view 247`
2. Extract the user story and acceptance criteria from the Issue Plan comment
3. Determine the deployed environment URL (dev environment, with option to override)
4. Dispatch the qa-tester agent with full context and Playwright MCP access in headed mode
5. Collect the structured report
6. Post the QA report as a comment on the issue
7. Handle results:
   - **All ACs pass, no bugs** — move issue to Done, close it
   - **Failures found** — create bug issues linked as sub-issues to the parent story. Leave the issue in QA status. Bugs go through the normal pipeline (planning, implementation, merge) and the story gets re-tested on a future `/qa` run.
   - **Gaps discovered** — offer to create new feature/story issues (not linked as sub-issues — they are new scope)

**QA queue: `/qa`** (no arguments)
1. Query the project board for all issues in QA status in the current iteration
2. Present the list: "Found 3 issues in QA: #247, #250, #253. Starting QA session."
3. Run the QA agent for each issue **sequentially** (not parallel — avoids data conflicts in the shared environment and lets the owner watch each one)
4. After each issue: post report, file bugs, move to Done if clean
5. After all issues: print session summary ("3 issues tested, 2 passed, 1 had 2 bugs filed")

**Re-testing flow:** When bugs filed from QA get fixed and merged, the parent story stays in QA. The next `/qa` run picks it up again. If all ACs now pass and no open bug sub-issues remain, the story moves to Done.

### 5. What This Does Not Include

- **Playwright E2E regression suite** — a separate effort for automated regression tests that run in CI. This design is specifically about story-level QA validation.
- **Agent-driven QA in GitHub Actions** — ruled out due to runner minute costs and the desire for live monitoring. Can be revisited later.
- **Exploratory testing beyond acceptance criteria** — the agent verifies ACs and notes incidental issues, but does not do open-ended exploration.
