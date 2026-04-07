# Cluster 1 — Walk-up waitlist redesign — session handoff

Copy the block below into a fresh Claude Code session to resume this work. The prompt is **state-independent** — it asks the new session to discover where the branch is from git/files/tasks rather than embedding a snapshot, so it stays valid as the work progresses.

---

```
You are picking up Cluster 1 of the Teeforce Fieldstone redesign rollout. We
are working in the existing branch `chore/fieldstone-cluster-1-walkup-waitlist`
on the `benjamingolfco/teeforce` repo. The brainstorming and spec phase is
complete. Plan and implementation may be partial or unstarted — discover
current state from the files and git, do not assume.

## Bootstrap sequence (do in this order, do not skip)

1. Switch to the branch:
   `git switch chore/fieldstone-cluster-1-walkup-waitlist`

2. Read these files in this order, end to end:
   - `docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md`
     — the Cluster 1 design spec (the source of truth for what to build)
   - `docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`
     — the Fieldstone design language and AppShell foundation
   - `docs/superpowers/plans/2026-04-06-operator-admin-redesign-foundation.md`
     — the foundation implementation plan (so you understand what's
     already built and available to use)
   - `.claude/rules/frontend/react-conventions.md`
     — especially the "Theming shadcn components" section

3. Discover current state:
   - `git status` and `git log --oneline -20` to see what's been committed
     on this branch
   - `ls docs/superpowers/plans/` to see if a Cluster 1 implementation
     plan exists yet (it would be named like
     `2026-04-XX-walkup-waitlist-cluster-1.md`)
   - `gh issue view 382` to confirm the cluster issue is still open and
     read any new comments
   - If the implementation plan exists, read it and find the next
     unchecked `- [ ]` task

4. Pick up where the branch left off:
   - **No plan yet** → invoke `superpowers:writing-plans` to produce one
     from the Cluster 1 spec
   - **Plan exists, implementation incomplete** → invoke
     `superpowers:subagent-driven-development` (preferred) or
     `superpowers:executing-plans` to continue from the next unchecked task
   - **Implementation looks complete** → run lint/test/build, do the
     manual `make dev` smoke from the spec, then prepare the PR

## GitHub tracking

- Parent epic: #381 (Operator/Admin redesign rollout) — labeled
  `agent/ignore`
- This cluster: #382 — labeled `agent/ignore`
- Other clusters: #383 (Cluster 2), #384 (Cluster 3), #385 (Cluster 4),
  all `agent/ignore`
- All clusters are excluded from the autonomous agent pipeline by design.
  We are driving this work manually through the brainstorm → spec → plan
  → execute flow.
- The PR for this cluster must include `Closes #382` in its body so the
  sub-issue closes automatically and the parent epic reflects progress.

## Hard rules (user-instructed, non-negotiable)

These override default behavior. Subagents do not inherit context, so when
you dispatch one you MUST repeat these rules in the dispatch prompt:

- **NO new unit tests.** Existing tests must keep passing. Update locators
  only when the redesign forces it. Behavior assertions are protected
  specifications.
- **NO new e2e tests.** Same rule.
- **NO new functionality.** Visual / structural only. No new endpoints,
  no new aggregations, no new fields, no new dialogs, no new actions.
  Every pixel of new UI must be backed by data that already exists in
  `WalkUpWaitlistTodayResponse`.
- **shadcn primitives in `src/web/src/components/ui/` are read-only.**
  Theme via CSS variables in `index.css`; new visual variants live in
  wrapper components. The exception is `StatusBadge` and `StatusChip`,
  which are project wrappers (not shadcn primitives) and are extensible.
  The Cluster 1 spec extends `StatusBadge` with three new variants —
  this is allowed and expected.
- **Right rail is out of scope for this page.** Do not render
  `<PageRightRail>` from `WalkUpWaitlist.tsx`.
- **`WaitlistShellLayout.tsx` is not modified in this cluster.** It is
  shared with `CoursePortfolio` (Cluster 4). All page-specific topbar
  content comes from `<PageTopbar>` slot contributions inside the page.

## Workflow rules

- Use `make dev` for visual verification. Subagents stop at
  lint/test/build and hand off for manual browser smoke.
- Don't write code until there is an approved implementation plan.
- Run `pnpm --dir src/web lint` and `pnpm --dir src/web test` after any
  TypeScript changes. Both must be clean before declaring done.
- Prefer batching subagents by phase rather than per-task — per-task is
  too expensive.
- Manual smoke states required before opening the PR: Inactive, Open,
  Closed, drawer-open, plus a sanity check on one golfer page and the
  tee sheet.

## When you finish

PR title: `feat(web): Fieldstone redesign — walk-up waitlist (Cluster 1)`
PR body must include:
- `Closes #382`
- A link to `docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md`
- Before/after screenshots of: Inactive, Open, Closed, drawer-open
- Confirmation that lint/test/build are clean and that the manual smoke
  states above were verified

Do not push to main. Do not skip hooks. Do not amend commits that have
already been pushed.
```

---

## How to use this

1. Open a new Claude Code session in the same `teeforce` working directory.
2. Paste the block above as your first message.
3. Claude will run the bootstrap sequence, figure out where the branch is, and pick up the next step.

## When to delete this file

When Cluster 1's PR merges and `#382` closes, this handoff is no longer needed. Delete the file as part of cleanup, or keep it as a template for future cluster handoffs (Clusters 2–4 will follow the same shape — just swap the spec path and issue numbers).
