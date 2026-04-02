---
name: qa
description: Run QA validation against deployed environment for user stories. Verifies acceptance criteria via Playwright browser (headless by default), posts reports, files bugs.
user-invocable: true
---

# QA Validation

Validate user stories against a deployed environment by walking through acceptance criteria in a browser (headless by default).

## Usage

- `/qa 247` — test a specific issue
- `/qa` — test all issues in QA status in the current iteration
- `/qa 247 --headed` — run with a visible browser window
- `/qa --url http://localhost:3000` — override the environment URL

Default is **headless** (no visible browser). Use `--headed` when you want to watch the tests run.

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
   - Read the `## Test Environment` section of `README.md` in the repo root to get the **Frontend** URL
   - Override via argument: `/qa 247 --url http://localhost:3000`
   - If the URL is not reachable, stop and tell the user

4. **Load test credentials:**
   - Read `.local/test-credentials.md` from the repo root
   - This file contains usernames, passwords, and roles for test accounts
   - Pass the full credentials content to the QA tester agent so it can authenticate

5. **Dispatch the QA tester agent** via the Agent tool:
   - `subagent_type: "qa-tester"`
   - Include in the prompt:
     - The issue number and title
     - The full story and acceptance criteria text
     - The environment URL
     - The test credentials (from `.local/test-credentials.md`)
     - Instruction to log in with the appropriate test account before testing (pick the role that matches the story's persona)
     - Instruction to use Playwright MCP tools in headless mode (or headed if `--headed` was passed)
   - Wait for the agent to return the QA report

6. **Post the report on the issue:**
   ```bash
   gh issue comment {number} --repo benjamingolfco/shadowbrook --body "{report}"
   ```
   Prefix the comment with the QA role icon:
   ```markdown
   ### 🔍 QA Tester — Validation Report for #{number}

   {report content}
   ```

7. **Handle results:**

   **All ACs pass:**
   - Check for open sub-issues:
     ```bash
     gh api repos/benjamingolfco/shadowbrook/issues/{number}/sub_issues --jq '[.[] | select(.state == "open")] | length'
     ```
   - If open sub-issues exist: tell the user "All ACs pass but {N} sub-issue(s) are still open. Leaving in QA."
   - If no open sub-issues: move issue to Done and close it
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
   - Link each bug as a sub-issue of the parent story:
     ```bash
     gh api repos/benjamingolfco/shadowbrook/issues/{parent}/sub_issues -X POST -F sub_issue_id={bug_id}
     ```
   - Leave the parent issue in QA status
   - Tell the user: "{N} bug(s) filed and linked to #{number}. Issue stays in QA."

   **Gaps discovered:**
   - Present the suggested story titles to the user
   - Ask if they want to create them as new issues (these are NOT sub-issues — they are new scope)

8. **Summarize to the user** what was done for this issue:
   - Whether all ACs passed or which failed
   - That the QA report was posted to the issue (link to the comment)
   - Whether the issue was moved to Done and closed, or left in QA
   - Any bugs filed or stories suggested

## Queue Mode

When no issue number is provided:

1. **Query the project board** for open issues in QA status in the current iteration:
   ```bash
   gh api graphql -f query='
     query {
       organization(login: "benjamingolfco") {
         projectV2(number: 1) {
           items(first: 100, query: "iteration:@current status:QA") {
             nodes {
               content {
                 ... on Issue { number title state }
               }
             }
           }
         }
       }
     }
   ' --jq '.data.organization.projectV2.items.nodes[] | select(.content.state == "OPEN") | .content'
   ```

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
- The story moves to Done only when all ACs pass AND no open sub-issues remain

## QA Bug Lifecycle

Bugs created by this skill:
- Are linked as sub-issues to the parent story
- Skip BA story refinement — the QA report provides the reproduction steps and screenshots
- Should be set to Ready status immediately (they are implementation-ready)
- Go through normal implementation pipeline from Ready onward
