# Walk-up Waitlist Redesign — Cluster 1

**Date:** 2026-04-07
**Branch:** `chore/fieldstone-cluster-1-walkup-waitlist`
**Tracking:** #382 (sub-issue of #381 — Operator/Admin redesign rollout)
**Foundation:** PR #380, [`docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`](2026-04-06-operator-admin-redesign-foundation-design.md)

## Summary

Cluster 1 of the Fieldstone redesign rollout. Restyle and restructure the operator-side walk-up waitlist page (`src/web/src/features/operator/pages/WalkUpWaitlist.tsx`) to use the Fieldstone design language inside the existing minimal `AppShell`. The page is the most-used surface for current phase-1 customers, so visual consistency with the redesigned tee sheet (PR #380) is the primary goal — operators who graduate from walk-up-only to the full operator app should feel they are using the same product.

The page's data, behavior, dialogs, hooks, and routing are unchanged. Only the layout and the visual treatment change. No new endpoints, no new aggregations, no new fields, no new unit or e2e tests.

`WaitlistShellLayout` is **not modified** in this cluster — the foundation PR already shimmed it onto `<AppShell variant="minimal">`, and it is also used by `CoursePortfolio` (a Cluster 4 page), so the contract stays frozen here. All visual additions for the walk-up waitlist page come from the page itself via `<PageTopbar>` slot contributions.

## Out of scope

- Any change to `WaitlistShellLayout.tsx`, `AppShell.tsx`, `CoursePortfolio.tsx`, or any foundation primitive.
- The right rail. The page does not render `<PageRightRail>`.
- Mobile / small-screen layout. The redesign is desktop-first; below ~1024px the topbar middle slot may squish but is not designed for. Deferred until usage data shows it matters.
- New product features (no new dialogs, no new fields, no new actions).
- New unit or e2e tests. Existing tests stay; locators are updated only where the redesign forces them.
- Date display in the topbar. The page is always "today" and the shell already supplies the course brand; adding a date is unnecessary noise.

## Section 1 — Architecture & topbar

### Shell

The page mounts inside `WaitlistShellLayout`, which is itself an `<AppShell variant="minimal">`. The shell already provides:

- The course-name brand on the far left of the topbar (rendered by `WaitlistShellLayout`'s `WaitlistBrand` component, passed as `brand` prop).
- The `UserMenu` on the far right of the topbar with the existing switch-course logic.
- An empty content region (`bg-paper`) below the topbar where the page renders.

None of that changes.

### Page topbar contributions

The page contributes content to the topbar via `<PageTopbar>` slot helpers, only in the **Open** and **Closed** states. Inactive, Loading, and Error states contribute nothing — the shell brand + `UserMenu` are the entire topbar.

**Left slot:** unused. The shell brand already occupies the far-left position; the page does not need to add anything next to it.

**Middle slot:** a horizontal group of three elements, separated by a small gap, vertically centered in the topbar (~24 px tall):

1. **Status chip** — `<StatusChip tone="green">OPEN</StatusChip>` in the Open state, `<StatusChip tone="gray">CLOSED</StatusChip>` in the Closed state. Uses the foundation `StatusChip` primitive as-is.
2. **Short code** — the waitlist short code rendered in `font-mono` with letter-spacing (~0.15em), height matched to the chip. Each character readable on its own — no special big-display treatment, just clean mono digits. To the right of the digits, a small ghost `<Button>` with the lucide `Copy` icon (or `Check` for two seconds after a successful copy) preserves today's copy-to-clipboard behavior. The success-tick state and the existing `data-testid="short-code"` are preserved.
3. **Queue trigger** — a borderless pill that opens the `QueueDrawer`. Shape: count number in `font-mono` (`text-ink` when count > 0, `text-ink-muted` when 0), the word "waiting" in Plex Sans muted, and a `ChevronDown` icon (lucide). Wrapped in `<DrawerTrigger asChild>`. `aria-label` preserved (`"N golfers waiting. Show queue"`).

The middle slot is the page's "live state" — what's happening right now at this waitlist. It is the only place on the page where the status, code, and queue count appear.

**Right slot:** a single `⋯` overflow `<DropdownMenu>` (existing shadcn primitive) sitting just left of the shell-rendered `UserMenu`. Trigger is a small ghost icon button (`MoreHorizontal` from lucide) with `aria-label="More actions"`.

Menu items:

| State  | Items |
|--------|-------|
| Open   | **Add golfer manually** → opens `AddGolferDialog`. **Print QR sign** → opens `<Dialog>` with `<QrCodePanel>`. **Close waitlist for today** → opens `CloseWaitlistDialog` (rendered with `text-destructive` styling on the menu item). |
| Closed | **Reopen waitlist** → opens `ReopenWaitlistDialog`. (Print and Add Golfer are hidden — they only make sense while open.) |

The menu items use shadcn `<DropdownMenuItem>` primitives, themed via tokens. No custom variant work.

### What the topbar contributions kill from today's page

- The page-body meta row (`[N waiting ⌄] · Print sign · Add golfer · Close waitlist`) — relocated entirely to topbar middle (queue trigger) and topbar right (overflow menu).
- The page-body `<h1>Walk-Up Waitlist</h1>` in Open and Closed states — identity is now carried by the topbar (course brand + status chip + code).
- The dual mobile/desktop render of the openings list — single layout only.

## Section 2 — Page body by state

The page is one component (`WalkUpWaitlist.tsx`) that branches on five states: Loading, Error, Inactive, Open, and Closed. The branching logic is preserved from today; the contents of each branch change.

### Loading

- Topbar: page contributes nothing.
- Body: a centered region with a Fieldstone-styled `<Skeleton>` block sized roughly to the openings grid (~80 px tall summary line, ~200 px tall list). The existing `aria-label="Loading walk-up waitlist"` is preserved on the wrapper for the existing test.
- No `<h1>`. No max-width container fight — the skeleton sits in the page's natural padding.

### Error

- Topbar: page contributes nothing.
- Body: centered Fieldstone card (~max-w-md), `font-display` heading "Couldn't load waitlist", muted Plex Sans error message body, outlined `<Button>` "Retry" wired to `todayQuery.refetch()`. Same shape as today, restyled.

### Inactive (no waitlist for today)

The shell brand + `UserMenu` are the entire topbar. The page renders no topbar contributions and no overflow menu — there is no waitlist to print, close, or add golfers to.

Body: a centered Fieldstone empty card (~max-w-md), white-on-paper, `border-border-strong`:

- `<h1 class="font-display">Walk-Up Waitlist</h1>` — the **only** state where this title appears.
- Plex Sans muted body copy (preserved verbatim from today): "Open the waitlist to let walk-up golfers join the queue. You'll post tee time openings throughout the day."
- One primary `<Button>` "Open Waitlist for Today", full-width within the card, ~h-11. Inherits the Fieldstone primary style (green-faint background + green text) from the token cascade — no custom styling.
- The 409-conflict message ("Waitlist is already open — try refreshing the page.") and the generic open-error message render below the button in `text-destructive` Plex Sans, exactly as today.

### Open

- Topbar: page contributes the full middle slot (status chip + code + queue trigger) and right slot (overflow menu) per Section 1.
- Body, top-to-bottom inside the page's natural padding:
  1. **Post Tee Time card** — see Section 4.
  2. **Today's Openings section** (the openings grid) — see Section 3.

There is no page-body header, no meta row, no `<h1>`. The body reads as `[Post Tee Time card] [Today's Openings grid]`.

### Closed

- Topbar: page contributes the middle slot (gray "CLOSED" chip + code + queue trigger; the queue trigger still works because the entries from when the waitlist was open remain readable in the response payload) and right slot (overflow menu containing only "Reopen waitlist").
- Body:
  1. **Closed banner** — a single thin notice strip at the top of the page body. Implemented as a div with `bg-canvas border-y border-border-strong px-4 py-2 text-sm text-ink-secondary`. Message: `"Waitlist closed. N golfer(s) were on the queue."` Token-based; no new component.
  2. **Today's Openings section** in **read-only** mode — see Section 3 for the `readOnly` flag.

The Post Tee Time card is **not rendered** in the Closed state — you can't post when closed.

### `if (!course)` guard

The existing "Select a course from the sidebar to manage the walk-up waitlist." guard is preserved and restyled to use Fieldstone tokens (`text-ink-muted` Plex Sans, centered in page padding). Wording unchanged.

## Section 3 — The openings grid

A new component `OpeningsGrid` replaces the existing `OpeningsList`. The new file lives at `src/web/src/features/operator/components/OpeningsGrid.tsx`. The old `OpeningsList.tsx` file is deleted (no other consumers).

### Component shape

```tsx
<OpeningsGrid
  openings={openings}
  readOnly={waitlist.status === 'Closed'}
  onCancel={handleCancelClick}
  cancellingId={cancelMutation.isPending ? cancellationTarget?.id ?? null : null}
/>
```

Same props as today's `OpeningsList` plus a `readOnly` boolean for the Closed state. No new data dependencies.

### Visual structure

```
TODAY'S OPENINGS                                      3 posted · 4/7 filled
──────────────────────────────────────────────────────────────────────────
TIME    STATUS      FILLED    GOLFERS
──────────────────────────────────────────────────────────────────────────
2:30    ●Filled     2/2       Smith, Lopez ×2
2:50    ●Open       1/2       Patel                                  [×]
3:10    ●Open       0/2       Waiting for golfers...                 [×]
3:30    ●Expired    1/2       Garcia
3:50    ●Cancelled  —         —
```

### Section header line

A row above the grid with two elements:

- **Left:** uppercase 10 px tracked muted "TODAY'S OPENINGS" — same idiom as TeeSheet's section header.
- **Right:** mono summary `"N posted · X/Y filled"`. The numbers come from filtering out `Cancelled` openings (same calculation as today's `OpeningsList`).

### Sticky column header row

`TIME / STATUS / FILLED / GOLFERS` in 10 px uppercase tracked muted, `border-b border-border`. Sticky to the top of the scrollable region (the page's main scroll container). Same idiom as TeeSheet's grid header.

### Grid rows

CSS grid layout with five tracks: TIME (~80 px), STATUS (~110 px), FILLED (~80 px), GOLFERS (1fr, truncating), CANCEL (~40 px). Row height ~40 px. Hover background `bg-muted/40`.

**Row variants**, derived from `opening.status`:

| Status     | Background  | Text          | Other treatment                                      |
|------------|-------------|---------------|------------------------------------------------------|
| Open       | `bg-paper`  | default ink   | Cancel `×` icon visible (unless `readOnly`)          |
| Filled     | `bg-paper`  | default ink   | Green left bar via `box-shadow: inset 3px 0 0 var(--green)` (same mechanic TeeSheet uses for current-time row) |
| Expired    | `bg-canvas` | `text-ink-muted` | No cancel icon                                    |
| Cancelled  | `bg-canvas` | `text-ink-muted` | FILLED cell shows `—`, GOLFERS cell shows `—`     |

### Cells

- **TIME** — `font-mono text-[12px] text-ink` (or `text-ink-muted` on faded variants). Uses `formatWallClockTime(opening.teeTime)` (existing helper).
- **STATUS** — `<StatusBadge status={mapOpeningStatus(opening.status)} />`. The `StatusBadge` primitive is extended with three new variants — see "StatusBadge extension" below.
- **FILLED** — `font-mono text-[12px]` rendering `${filled}/${opening.slotsAvailable}`, where `filled = opening.slotsAvailable - opening.slotsRemaining`. Cancelled rows show `—`.
- **GOLFERS** — Plex Sans 13 px, single-line `truncate`, comma-separated names with `×N` group sizes inline. Uses a `formatGolferNames` helper (moved out of `OpeningsList.tsx`). When `status === 'Open'` and `filledGolfers.length === 0`, renders `<span className="italic text-ink-muted">Waiting for golfers...</span>` — same string as today.
- **CANCEL cell** — small ghost icon button (lucide `X`), only on `Open` rows AND when `!readOnly`, always visible (not hover-gated). When `cancellingId === opening.id`, the button is disabled and the row gets `opacity-40` (matches today's behavior). `aria-label="Cancel opening at HH:MM"` preserved.

### StatusBadge extension

`StatusBadge` is a project wrapper in `src/web/src/components/ui/status-badge.tsx`, not a shadcn primitive — the convention allows extending it. Three new variants are added to the `StatusBadgeStatus` type and the `STATUS_STYLES` map:

| Variant     | Background      | Foreground      | Label       |
|-------------|-----------------|-----------------|-------------|
| `filled`    | `bg-green-light` | `text-green`   | "Filled"    |
| `expired`   | `bg-canvas`     | `text-ink-muted`| "Expired"   |
| `cancelled` | `bg-red-light`  | `text-red`      | "Cancelled" |

The existing `booked | open | waitlist | checkedin | noshowed` variants stay untouched.

### Helpers

A new file `src/web/src/features/operator/components/openingsHelpers.ts` exports:

```ts
import type { StatusBadgeStatus } from '@/components/ui/status-badge';

export function mapOpeningStatus(status: string): StatusBadgeStatus {
  switch (status) {
    case 'Open': return 'open';
    case 'Filled': return 'filled';
    case 'Expired': return 'expired';
    case 'Cancelled': return 'cancelled';
    default: return 'open';
  }
}

export function formatGolferNames(
  golfers: { golferName: string; groupSize: number }[]
): string {
  if (golfers.length === 0) return '';
  return golfers
    .map((g) => (g.groupSize > 1 ? `${g.golferName} (×${g.groupSize})` : g.golferName))
    .join(', ');
}
```

The helpers and the grid live in `features/operator/components/` (not in `components/ui/`) because they are domain-specific.

### Empty state

When `openings.length === 0`, the grid renders just the section header line + a single muted line "No openings posted yet." in the body area. Same copy as today, no nested empty card.

### What is removed from the old OpeningsList

- The `border rounded-lg divide-y` card/divider styling.
- The `md:hidden` mobile layout block — single layout only.
- The `data-testid="opening-row-mobile"` and `data-testid="opening-row-desktop"` test IDs — replaced with a single `data-testid="opening-row"` per row.
- The `formatGolferNames` function inside `OpeningsList.tsx` (moved to the helpers file).
- The `border-l-3 border-l-success` highlight on Filled rows (replaced by the green-bar inset shadow).

## Section 4 — QueueDrawer, PostTeeTimeForm, dialogs

### QueueDrawer

`src/web/src/features/operator/components/QueueDrawer.tsx`. Same component, same `<Drawer>` primitive, same trigger contract. Restyled internally; the trigger button is restyled to fit inside the topbar middle slot.

**Trigger button** (rendered into the topbar middle slot, wrapped by `<DrawerTrigger asChild>`):

- Borderless pill, ~24 px tall to match the StatusChip next to it.
- Layout: `flex items-center gap-1.5`.
- Children: count `<span class="font-mono">N</span>` (`text-ink` when N > 0, `text-ink-muted` when 0), then `<span class="text-sm text-ink-muted">waiting</span>`, then `<ChevronDown class="h-4 w-4 text-ink-muted">`.
- `aria-label` preserved (`"N golfers waiting. Show queue"`).

**Drawer content**:

- Drawer title `<DrawerTitle>` styled with `font-display` (16 px), `"N golfer(s) waiting"`.
- List rows in a `border-y` container, each row `border-b border-border`, padding `px-3 py-2`:
  - Mono index `01`, `02`, ... in `font-mono text-[12px] text-ink-muted` on the left (~28 px wide column).
  - Golfer name in Plex Sans 14 px ink, with `×N` group size inline in `text-ink-muted text-xs`.
  - Joined-at time on the right in `font-mono text-[11px] text-ink-muted`.
  - Remove button: small ghost text "Remove" in `text-destructive`, right of the joined-at, only when `isOpen` is true (preserves today's `isOpen` prop semantics — Open state passes `true`, Closed state passes `false` so historical entries are read-only).
- Empty state: "Queue is empty" in `text-ink-muted` Plex Sans, centered.
- Removes `divide-y` in favor of explicit `border-b border-border` per row.

**Closed state behavior:** the Open and Closed states both render the queue trigger in the topbar middle slot. In Closed state, the page passes `isOpen={false}` to the drawer so the Remove buttons hide automatically. Operators can still see the historical queue but can't modify it.

### PostTeeTimeForm

`src/web/src/features/operator/components/PostTeeTimeForm.tsx`. Same file, light restyling. No structural change. No prop changes. No tests should need updates from this file alone.

Restyle moves:

- Card explicitly uses `border-border-strong` and `bg-white` (currently `border-border/80 shadow-sm`, which works via tokens but is less explicit about the Fieldstone "panel" feel).
- Card header label changed from `<p class="text-base font-semibold">Post Tee Time</p>` to an uppercase 11 px tracked muted `<p class="text-[11px] uppercase tracking-wider text-ink-muted">POST TEE TIME</p>`, matching the openings grid section header idiom.
- Time `<Input type="time">` adds `font-mono` so the digits read mono.
- Slot picker buttons (1/2/3/4) keep their structure. The selected state uses `bg-primary text-primary-foreground` which already resolves to green-faint/green via the foundation token cascade — no class changes needed.
- Submit button keeps its structure and inherits Fieldstone styling from `<Button>`.
- "Posted!" success feedback unchanged in behavior; visually it inherits the new Button styles.
- Validation error messages and the 409 duplicate-opening error message unchanged.

### Dialogs

`AddGolferDialog`, `CloseWaitlistDialog`, `ReopenWaitlistDialog`, `RemoveGolferDialog`, `CancelOpeningDialog`, and the inline `<Dialog>` wrapping `QrCodePanel` — **no source edits**. They use shadcn `<Dialog>`, `<Button>`, `<Input>`, `<Label>`, `<Form>` primitives that are themed via tokens and pick up Fieldstone automatically.

If any specific dialog file has hardcoded color classes (`text-amber-600`, `bg-blue-500`, `text-red-700`, etc.) that fight the new tokens, those are converted to token-based classes (`text-orange`, `bg-blue-light`, `text-red`) during implementation — case-by-case grep, no structural changes. This is the "obvious offenders" cleanup the foundation spec mentions in its risk section.

### QrCodePanel

`src/web/src/features/operator/components/QrCodePanel.tsx` — used unchanged, just triggered from a different place (the overflow menu's "Print QR sign" item instead of the page meta row).

## Section 5 — Files, tests, and rollout

### Files created

```
src/web/src/features/operator/components/
├── OpeningsGrid.tsx               # New grid component (replaces OpeningsList visually)
├── openingsHelpers.ts             # mapOpeningStatus, formatGolferNames
└── WalkUpWaitlistTopbar.tsx       # Page-internal: renders <PageTopbar middle={...} right={...}>
                                   #   for the Open and Closed states
```

`WalkUpWaitlistTopbar.tsx` exists purely to keep `WalkUpWaitlist.tsx` from blowing up with PageTopbar slot construction. It takes the props it needs (waitlist, entries, drawer state, callbacks) and renders the `<PageTopbar>` element. Same pattern as `TeeSheetTopbarTitle.tsx` and `TeeSheetDateNav.tsx` from the foundation PR.

### Files modified

```
src/web/src/features/operator/pages/WalkUpWaitlist.tsx
  - Replace the page header meta row + <h1> (in Open/Closed states) with <WalkUpWaitlistTopbar>
  - Replace OpeningsList import with OpeningsGrid
  - Pass readOnly={waitlist.status === 'Closed'} to OpeningsGrid in Closed state
  - Restructure Closed state to render the same body shell as Open with banner + read-only grid
  - Hide Post Tee Time card in Closed state
  - Keep all existing dialog state and mutation handlers exactly as-is
  - Restyle the !course guard
  - Restyle Loading and Error branches

src/web/src/features/operator/components/QueueDrawer.tsx
  - Restyle drawer content (mono indices, ink/muted typography, border-b rows)
  - Restyle the trigger button to be a borderless ~24 px pill (so it sits cleanly in topbar middle)

src/web/src/features/operator/components/PostTeeTimeForm.tsx
  - Restyle card to border-border-strong / bg-white explicitly
  - Change the card header to uppercase tracked muted
  - Add font-mono to the time Input
  - No structural / behavior changes

src/web/src/components/ui/status-badge.tsx
  - Add 'filled' | 'expired' | 'cancelled' to StatusBadgeStatus
  - Add corresponding entries to STATUS_STYLES
```

### Files deleted

```
src/web/src/features/operator/components/OpeningsList.tsx
```

### Files NOT touched

- `WaitlistShellLayout.tsx` — already shimmed in PR #380; also used by `CoursePortfolio` (Cluster 4).
- `AppShell.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, `AppShellContext.tsx` — foundation primitives.
- `StatusChip.tsx`, `PanelSection.tsx` — foundation primitives.
- `index.css` / Fieldstone tokens — no token changes.
- All dialog files (`AddGolferDialog`, `CloseWaitlistDialog`, `ReopenWaitlistDialog`, `RemoveGolferDialog`, `CancelOpeningDialog`) unless a hardcoded color class needs converting.
- `useWalkUpWaitlist.ts`, `useWaitlist.ts`, all backend code, all types in `src/web/src/types/waitlist.ts` — no data layer changes.
- `QrCodePanel.tsx` — used unchanged.
- `CoursePortfolio.tsx` — Cluster 4.

### Tests

Per the cluster's "no new tests" rule, no `.test.tsx` or `.spec.ts` files are created. Existing tests are touched **only** when their locators break.

**Tests likely to need locator updates:**

- `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`:
  - Assertions that find the `<h1>Walk-Up Waitlist</h1>` via `getByRole('heading', { name: /Walk-Up Waitlist/i })` need to be scoped to the Inactive state (where the heading still exists) or removed if they were testing presence in Open/Closed.
  - Assertions that find the meta row links by text (`/Print sign/`, `/Add golfer/`, `/Close waitlist/`) update to find them inside the overflow menu — open via `userEvent.click(screen.getByLabelText(/More actions/i))` then assert `getByRole('menuitem', { name: ... })`.
  - Assertions that find the queue count text in the page body need to find it in the topbar middle (still visible via `screen.getByText`, just in a different DOM region).
  - Any locator using `data-testid="opening-row-desktop"` updates to `opening-row`.
  - **Behavior assertions stay unchanged**: opening the waitlist, closing, reopening, posting, cancelling, removing, copying the short code, the 409 conflict messages, the empty state copy. Those are specifications and the behavior didn't change.
- `src/web/src/features/operator/__tests__/OperatorFeature.test.tsx` — references `WaitlistShellLayout` to assert it renders when `full_operator_app=false`. Almost certainly unaffected because it tests the routing decision, not the page contents. Verified during implementation.

**E2E tests:** the only e2e folder is `e2e/tests/walkup`, which is golfer-side (joining the queue from a phone). If any operator-side e2e exists in there, locators are updated using the same rules. No new e2e specs.

### Manual smoke (per project rule)

After implementation, run `make dev` and click through:

- The walk-up waitlist route in **Inactive** state — verify the empty card renders, the "Open Waitlist for Today" button works.
- Same page in **Open** state — verify topbar middle shows OPEN chip + code + queue count, overflow menu has three items, openings grid renders, posting a tee time works, cancelling an opening works, removing a queued golfer works, copy code works, drawer opens.
- Same page in **Closed** state — verify gray chip, banner, read-only grid, overflow menu has only "Reopen waitlist".
- One golfer page (e.g., walkup join flow) — verify nothing broke from the cluster.
- Tee sheet — verify foundation PR #380's tee sheet is unchanged.

### Rollout

One PR titled `feat(web): Fieldstone redesign — walk-up waitlist (Cluster 1)`. Body uses `Closes #382` so the cluster sub-issue closes on merge and the parent epic #381 reflects the progress automatically. Includes before/after screenshots of: Inactive, Open, Closed, drawer-open.

## Section 6 — Risks

1. **`WalkUpWaitlist.test.tsx` locator churn.** The test file is the largest single source of touch in this cluster. **Mitigation:** read it in full at the start of implementation and update locators in one pass. Behavior assertions are protected.
2. **Hardcoded color classes in dialogs.** Specific dialogs may use Tailwind palette colors (`amber`, `blue`, `red`) that don't map cleanly to Fieldstone. **Mitigation:** grep `features/operator/components/*Dialog.tsx` for hardcoded color class patterns before declaring done; convert to tokens.
3. **`StatusBadge` extension reads as new functionality.** Adding three variants is technically API surface growth on a foundation primitive. **Mitigation:** the spec lists this explicitly and the foundation spec already established that `StatusBadge` is a project wrapper (not a shadcn primitive) and is extensible. The new variants serve only the openings grid.
4. **Topbar middle slot crowding at narrow widths.** Status chip + code + copy + queue trigger is four elements in one slot. **Mitigation:** acceptable per the deferred small-screen scope; revisit when usage data arrives.
5. **Inactive state still renders an `<h1>` while Open/Closed do not.** Inconsistent at first glance, but justified — the empty card needs identity since the topbar contributes nothing in Inactive. **Mitigation:** documented here so reviewers don't flag it.

## Done criteria

- [ ] `WalkUpWaitlist.tsx` no longer renders an `<h1>` in Open or Closed states.
- [ ] `WalkUpWaitlist.tsx` no longer renders the page-body meta row.
- [ ] `WalkUpWaitlistTopbar.tsx` exists and renders `<PageTopbar middle right>` with the status chip, short code (mono + copy), queue trigger (drawer trigger), and overflow menu.
- [ ] `OpeningsGrid.tsx` exists and renders the four-column grid with the row variants described in Section 3.
- [ ] `OpeningsList.tsx` is deleted.
- [ ] `openingsHelpers.ts` exists with `mapOpeningStatus` and `formatGolferNames`.
- [ ] `StatusBadge` has the three new variants (`filled`, `expired`, `cancelled`).
- [ ] `QueueDrawer.tsx` is restyled (mono indices, ink/muted typography, restyled trigger button).
- [ ] `PostTeeTimeForm.tsx` is restyled (uppercase tracked card header, mono time input, explicit border-border-strong).
- [ ] Closed state renders the banner + read-only `OpeningsGrid` (no Post Tee Time card).
- [ ] Inactive state renders the centered Fieldstone empty card with the only `<h1>` on the page.
- [ ] `pnpm --dir src/web lint` clean.
- [ ] `pnpm --dir src/web test` clean (existing tests pass; locator updates only where forced).
- [ ] No new test files.
- [ ] No changes to backend, hooks, types, AppShell, foundation primitives, `WaitlistShellLayout`, or `CoursePortfolio`.
- [ ] Manual smoke (`make dev`) green for all five page states + golfer page + tee sheet sanity check.
- [ ] PR opened with `Closes #382`, before/after screenshots, and a link to this spec.
