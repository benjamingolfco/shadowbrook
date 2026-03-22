---
name: github-project
description: GitHub project management commands, field IDs, status option IDs, issue dependencies, labels, milestones, and sprint planning. Use when creating/editing issues, managing project board, setting status/priority/size, linking sub-issues, or working with the agent pipeline.
---

# GitHub Project Management

Repo: `benjamingolfco/shadowbrook` | Project: #1 under `benjamingolfco` org

| Action | Command |
|--------|---------|
| Create issue | `gh api repos/benjamingolfco/shadowbrook/issues -X POST -f title="..." -f body="..." -f type="Feature"` |
| List issue types | `gh api orgs/benjamingolfco/issue-types` |
| Add labels | `gh issue edit {number} --add-label "label1,label2"` |
| Add to project | `gh project item-add 1 --owner benjamingolfco --url {issue_url}` |
| Set project field | `gh project item-edit --project-id {id} --id {item_id} --field-id {field_id} --single-select-option-id {option_id}` |
| Link sub-issue | `gh api repos/benjamingolfco/shadowbrook/issues/{parent}/sub_issues -X POST -F sub_issue_id={child_id}` |
| View issue | `gh issue view {number}` |
| List issues | `gh issue list --state open` |
| List project items | `gh project item-list 1 --owner benjamingolfco` |
| List project fields | `gh project field-list 1 --owner benjamingolfco` |
| Pin issue comment | `gh api repos/benjamingolfco/shadowbrook/issues/comments/{comment_id}/pin -X PUT` |
| Unpin issue comment | `gh api repos/benjamingolfco/shadowbrook/issues/comments/{comment_id}/pin -X DELETE` |

Notes:
- Use `-F` (not `-f`) for integer fields (e.g., `sub_issue_id`)
- Issue types: Task, Bug, Feature, User Story

## Project Field IDs

**Status field:** `PVTSSF_lADOD3a3vs4BOVqOzg9EexU`

Issues with **no status set** are the backlog — new/untouched issues.

| Status | Option ID |
|--------|-----------|
| Needs Story | `4e3e5768` |
| Ready | `e82ffa87` |
| Implementing | `40275ace` |
| Awaiting Owner | `4fd57247` |
| Done | `b9a85561` |

**Priority field:** `PVTSSF_lADOD3a3vs4BOVqOzg9Ee2Y`

| Priority | Option ID |
|----------|-----------|
| P0 | `3c18c090` |
| P1 | `9b7578d1` |
| P2 | `969fb3b8` |

**Size field:** `PVTSSF_lADOD3a3vs4BOVqOzg9Ee2c`

| Size | Option ID |
|------|-----------|
| XS | `370e9b19` |
| S | `e55bf2b0` |
| M | `83ebb89c` |
| L | `f8aa8288` |
| XL | `4482bca2` |

**Iteration field:** `PVTIF_lADOD3a3vs4BOVqOzg9Ee2k`

## GitHub Issue Dependencies

GitHub native issue dependencies for tracking blocked-by/blocking relationships:

| Action | Command |
|--------|---------|
| List blockers | `gh api repos/benjamingolfco/shadowbrook/issues/{N}/dependencies/blocked_by` |
| Add blocker | `gh api repos/benjamingolfco/shadowbrook/issues/{N}/dependencies/blocked_by -X POST -F issue_id={blocking_issue_node_id}` |
| List what this blocks | `gh api repos/benjamingolfco/shadowbrook/issues/{N}/dependencies/blocking` |
| Remove blocker | `gh api repos/benjamingolfco/shadowbrook/issues/{N}/dependencies/blocked_by/{dependency_id} -X DELETE` |

## Milestones & Iterations

- **Milestones** define roadmap scope — the planning workflow only processes issues in the active milestone (earliest-due open milestone)
- **Iterations** define sprint scope — the implementation workflow only works on issues in the current iteration (GitHub Projects Iteration field)
- **Dependencies** define execution order — the implementation workflow dispatches unblocked issues first

## Story Points

The Architect estimates story points using the Fibonacci sequence (1, 2, 3, 5, 8, 13, 21) during the planning phase. Points are added to the Issue Plan comment and used for sprint capacity planning.

## Issue Labels

| Label | When to Apply |
|-------|--------------|
| `golfers love` | Feature/story where the golfer directly experiences or benefits from the functionality |
| `course operators love` | Feature/story where the course operator directly experiences or benefits from the functionality |
| `v1` | Core MVP — must ship for launch |
| `v2` | Enhanced — post-MVP improvements |
| `v3` | Future — long-term roadmap items |
| `agent/ignore` | Escape hatch — both workflows skip issues with this label |

Apply audience labels based on who benefits — many features get **both** `golfers love` and `course operators love` (see "Features Both Golfers AND Courses Will Love" in the roadmap). Always apply exactly one version label (`v1`, `v2`, or `v3`) based on the roadmap tier.
