# Dev Task List — Design

## Problem

When an architect's plan requires multiple implementation agents (backend + frontend), the PM must infer which agents to dispatch from a long technical plan comment. On issue #145, the PM skipped the frontend developer entirely, stalling the pipeline.

## Solution

The architect creates a **Dev Task List** comment on the issue — a structured checklist grouped by agent. The PM reads it to determine which agents to dispatch and in what order. Implementation agents check off items as they complete work. Both the PM status comment and the dev task list comment are **pinned** to the issue for easy access.

## Comment Format

```markdown
## Dev Task List

### Backend Developer
- [ ] Create Tenant entity with org name and contact fields
- [ ] Add required TenantId FK to Course entity
- [ ] Implement POST /tenants endpoint with validation
- [ ] Implement GET /tenants (list with courseCount) endpoint
- [ ] Implement GET /tenants/{id} (detail with courses) endpoint
- [ ] Write integration tests for all tenant endpoints

### Frontend Developer
- [ ] Create Tenant TypeScript type
- [ ] Create TanStack Query hooks for tenant API
- [ ] Build TenantCreate page (registration form)
- [ ] Build TenantList page (list view with course counts)
- [ ] Add routes for /admin/tenants and /admin/tenants/new
```

When the UX designer is dispatched in parallel, they add their own section:

```markdown
### UX Designer
- [x] Post interaction spec for tenant registration flow
```

## Responsibilities

| Actor | Responsibility |
|-------|---------------|
| **Architect** | Creates the dev task list comment after posting the technical plan. Groups tasks by agent. Pins the comment. |
| **UX Designer** | Adds a UX Designer section to the dev task list if dispatched in parallel. If UX finishes before the architect, creates the comment; architect adds to it. |
| **Implementation agents** | Check off items (`- [x]`) as they complete them. Do not add new items — if scope expands, hand back to PM. |
| **PM** | Reads the dev task list to determine which agents still have unchecked items. Dispatches agents in order. After all items are checked, advances to CI Pending. |

## PM Routing Logic

When the PM needs to decide what to do after an implementation agent hands back:

1. Find the dev task list comment (heading: `## Dev Task List`).
2. Parse each agent section for unchecked items (`- [ ]`).
3. If another agent section has unchecked items, dispatch that agent.
4. If all items are checked, advance to CI Pending.

## Pinning

Both key comments are pinned to every active issue for easy access:

- **PM Status comment** (`## PM Status`) — pinned by the PM when created.
- **Dev Task List comment** (`## Dev Task List`) — pinned by the architect (or UX designer) when created.

Pin API: `PUT /repos/{owner}/{repo}/issues/comments/{comment_id}/pin`

Pinning is idempotent — calling it on an already-pinned comment is a no-op. Agents should always pin after creating or first editing these comments to ensure they stay pinned.

## Changes Required

1. **Agent pipeline skill** — Add Dev Task List section describing the comment format, who creates it, and how agents interact with it.
2. **Architect agent** — Instruct to create and pin the dev task list comment after posting the technical plan.
3. **UX Designer workflow** (in agent-pipeline skill) — Instruct to add UX section to dev task list.
4. **Implementation agents** (backend, frontend, devops) — Instruct to check off items as they complete work.
5. **PM agent** — Add routing logic that reads the dev task list. Add pin instructions for PM status comment.
6. **CLAUDE.md** — Add pin/unpin commands to GitHub Project Management table.
