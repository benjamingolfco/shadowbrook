---
name: Fieldstone Redesign — Phase 5 TeeSheet components
description: New tee sheet components added in the operator/admin redesign foundation (branch chore/frontend-redesign)
type: project
---

Phase 5 of the Fieldstone redesign is complete on branch `chore/frontend-redesign`. All prior phases (1–4) were already committed.

**New files in `src/web/src/features/operator/components/`:**
- `teeSheetHelpers.ts` — `mapTeeTimeStatus`, `getInitials`
- `PlayerAvatar.tsx` — initials avatar with green/orange/gray tone variants
- `EmptySlot.tsx` — dash placeholder for empty cells
- `NowMarker.tsx` — "Now" divider line using `formatWallClockTime`
- `PlayerCell.tsx` — combines PlayerAvatar + name text
- `TeeSheetTopbarTitle.tsx` — course name + date for topbar left slot
- `TeeSheetDateNav.tsx` — prev/today/next buttons + date input
- `TeeSheetRow.tsx` — single grid row (past/current/default variants)
- `TeeSheetGrid.tsx` — full grid with sticky header and NowMarker placement

**Modified:** `src/web/src/features/operator/pages/TeeSheet.tsx` — rewritten to use `<PageTopbar>` + new grid components.

**Why:** Proof page for the Fieldstone design language per `docs/superpowers/plans/2026-04-06-operator-admin-redesign-foundation.md`.

**How to apply:** When working on this branch or follow-up cluster PRs, these components are available. The `TeeSheetGrid` consumes `TeeSheetRowSlot[]` and a `now` ISO string. The `NowMarker` uses `formatWallClockTime` (wall-clock, not UTC-converted).

**Type issue found:** `split('-').map(Number)` returns `(number | undefined)[]` in strict mode — use indexed access with nullish fallbacks instead of array destructuring. Fixed in `TeeSheetDateNav.tsx`.

**Backend build note:** `dotnet build teeforce.slnx` fails with MSB4276 workload resolver errors (missing SDK dirs) — this is a pre-existing environment issue, not caused by frontend changes. 0 actual C# errors.
