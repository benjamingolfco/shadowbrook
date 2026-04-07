# Cluster 2 — Admin CRUD redesign — session handoff

Copy the block below into a fresh Claude Code session to resume this work. The prompt is **state-independent** — it asks the new session to discover where the branch is from git/files/tasks rather than embedding a snapshot, so it stays valid as the work progresses.

---

```
You are picking up Cluster 2 of the Teeforce Fieldstone redesign rollout. We
are working in the existing branch `chore/fieldstone-cluster-2-admin-crud`
on the `benjamingolfco/teeforce` repo. The brainstorming and spec phase is
complete. Plan and implementation may be partial or unstarted — discover
current state from the files and git, do not assume.

## Bootstrap sequence (do in this order, do not skip)

1. Switch to the branch:
   `git switch chore/fieldstone-cluster-2-admin-crud`

2. Read these files in this order, end to end:
   - `docs/superpowers/specs/2026-04-08-admin-crud-cluster-2-design.md`
     — the Cluster 2 design spec (the source of truth for what to build)
   - `docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`
     — the Fieldstone design language and AppShell foundation
   - `docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md`
     — Cluster 1 spec, useful as a precedent for shape (topbar slots,
     `<PageTopbar>` usage, hardcoded color sweep, locator update strategy)
   - `.claude/rules/frontend/react-conventions.md`
     — especially the "Theming shadcn components" section

3. Discover current state:
   - `git status` and `git log --oneline -20` to see what's been committed
     on this branch
   - `ls docs/superpowers/plans/` to see if a Cluster 2 implementation
     plan exists yet (it would be named like
     `2026-04-XX-admin-crud-cluster-2.md`)
   - `gh api repos/benjamingolfco/teeforce/issues/383 --jq '{state, title}'`
     to confirm the cluster issue is still open
   - `gh pr list --head chore/fieldstone-cluster-2-admin-crud --state all`
     to see if a PR already exists for this branch
   - If the implementation plan exists, read it and find the next
     unchecked `- [ ]` task

4. Pick up where the branch left off:
   - **No plan yet** → invoke `superpowers:writing-plans` to produce one
     from the Cluster 2 spec
   - **Plan exists, implementation incomplete** → invoke
     `superpowers:subagent-driven-development` (preferred) or
     `superpowers:executing-plans` to continue from the next unchecked task
   - **Implementation looks complete** → run lint/test/build, do the
     manual `make dev` smoke from the spec, then prepare the PR

## GitHub tracking

- Parent epic: #381 (Operator/Admin redesign rollout) — labeled
  `agent/ignore`
- This cluster: #383 — labeled `agent/ignore`
- Other clusters: #382 (Cluster 1 — shipped in PR #386), #384 (Cluster 3),
  #385 (Cluster 4), all `agent/ignore`
- All clusters are excluded from the autonomous agent pipeline by design.
  We are driving this work manually through the brainstorm → spec → plan
  → execute flow.
- The PR for this cluster must include `Closes #383` in its body so the
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
  No new tile labels, no new table columns, no new tab counts.
- **shadcn primitives in `src/web/src/components/ui/` are read-only.**
  Theme via CSS variables in `index.css` (the project uses Tailwind v4
  with `@theme inline`, NOT `tailwind.config.js`). New visual variants
  live in wrapper components.
- **No new primitives in `components/ui/`.** Cluster 2's helpers
  (`StatTile`, `DetailTitle`) are domain-scoped and live under
  `features/admin/components/`. They are not foundation primitives.
- **Right rail is out of scope for this cluster.** Do not render
  `<PageRightRail>` from any of the nine admin pages.
- **`OperatorLayout.tsx` and `WaitlistShellLayout.tsx` are not modified.**
  They are Cluster 4 / shipped territory. The only layout file touched
  in this cluster is `AdminLayout.tsx`, which is **deleted entirely**.
- **`AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, and
  `PageRightRail.tsx` are foundation primitives — frozen here.**

## Foundation extension to land in this cluster

The spec's Section 6 introduces two new foundation tokens in
`src/web/src/index.css`:

```css
@theme inline {
  --shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.05);
  --shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
}
```

Values are stock Tailwind defaults — visual surface area is zero in this
PR. Future clusters can flip Fieldstone shadows in one place. Call this
out explicitly in the PR description.

## Workflow rules

- Use `make dev` for visual verification. Subagents stop at
  lint/test/build and hand off for manual browser smoke.
- Don't write code until there is an approved implementation plan.
- Run `pnpm --dir src/web lint` and `pnpm --dir src/web test` after any
  TypeScript changes. Both must be clean before declaring done.
- Prefer batching subagents by phase rather than per-task — per-task is
  too expensive.
- Manual smoke states required before opening the PR (per the spec's
  Section 7):
  - All nine admin pages (Org/Course/User × List/Detail/Create)
  - One operator page (e.g. tee sheet) — sanity check
  - One golfer page (e.g. walkup join) — sanity check that the new
    shadow tokens (at stock values) didn't move anything

## When you finish

PR title: `feat(web): Fieldstone redesign — admin CRUD (Cluster 2)`
PR body must include:
- `Closes #383`
- A link to `docs/superpowers/specs/2026-04-08-admin-crud-cluster-2-design.md`
- Before/after screenshots of one page from each pattern: one List
  (e.g. OrgList), one Detail (e.g. OrgDetail showing the tabs), one
  Create (e.g. OrgCreate)
- Confirmation that lint/test/build are clean and that the manual smoke
  states above were verified
- An explicit callout that this PR introduces `--shadow-sm` and
  `--shadow` foundation tokens at stock values

Do not push to main. Do not skip hooks. Do not amend commits that have
already been pushed.
```

---

## How to use this

1. Open a new Claude Code session in the same `teeforce` working directory.
2. Paste the block above as your first message.
3. Claude will run the bootstrap sequence, figure out where the branch is, and pick up the next step.

## When to delete this file

When Cluster 2's PR merges and `#383` closes, this handoff is no longer needed. Delete the file as part of cleanup, or keep it as a template for Cluster 3 / 4 handoffs (they will follow the same shape — just swap the spec path and issue numbers).
