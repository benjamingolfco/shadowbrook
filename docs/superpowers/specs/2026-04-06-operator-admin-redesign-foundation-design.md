# Operator/Admin Redesign — Foundation

**Date:** 2026-04-06
**Branch:** `chore/frontend-redesign`
**Scope:** operator + admin surfaces only. Golfer-facing flows out of scope.

## Summary

Establish a "Fieldstone" design language for Teeforce's operator and admin surfaces and prove it on the operator tee sheet. The foundation ships design tokens, fonts, a unified `AppShell` (with full and minimal variants), three small primitives, and a redesigned `TeeSheet` page. Existing layouts (`OperatorLayout`, `AdminLayout`, `WaitlistShellLayout`) are rewritten as thin wrappers around `AppShell` so the router does not move. All other operator/admin pages migrate in four follow-up clusters.

The aesthetic is locked from `docs/mockups/shadowbrook-theme-b.html`: warm paper canvas, dark ink sidebar, sage green and burnt orange accents, Libre Baskerville (display) + IBM Plex Sans (body) + IBM Plex Mono (numerics). No new functionality, no new endpoints, no new aggregations — every visual element in the mockup that requires data we do not currently have is either defined as an unused primitive or dropped entirely.

## Out of scope

- Golfer-facing layouts and pages (`GolferLayout`, `features/walk-up`, `features/walkup`, `features/walkup-qr`)
- New product features, new endpoints, new fields on existing models
- Visual regression infrastructure
- Golfer page redesigns triggered by token cascade — pinned with overrides if visually broken, not redesigned

## Section 1 — Design tokens & theming

### CSS variables (Fieldstone palette)

Defined in `src/web/src/index.css` under `:root`, copied verbatim from the mockup:

```
--canvas: #f4f2ee;       --paper: #faf9f7;        --white: #ffffff;
--ink: #1c1a18;          --ink-secondary: #4a4742; --ink-muted: #8c8880; --ink-faint: #c8c4bc;
--border: #e0dcd4;       --border-strong: #c8c4bc;
--green: #2e6b42;        --green-mid: #3d8a57;     --green-light: #d4ead9; --green-faint: #edf6f0;
--orange: #c45e1a;       --orange-mid: #e07035;    --orange-light: #f5dece; --orange-faint: #fdf3ec;
--red-light: #fce8e8;    --red: #c0392b;
--blue-light: #ddeaf8;   --blue: #2563a8;
```

### shadcn semantic mapping

In the same `:root` block, repoint shadcn's semantic tokens to the Fieldstone palette so all stock primitives inherit the look without source edits:

```
--background: var(--paper);
--foreground: var(--ink);
--card: var(--white);
--card-foreground: var(--ink);
--popover: var(--white);
--popover-foreground: var(--ink);
--muted: var(--canvas);
--muted-foreground: var(--ink-muted);
--accent: var(--canvas);
--accent-foreground: var(--ink);
--border: var(--border);
--input: var(--border);
--ring: var(--green-mid);
--primary: var(--green-faint);
--primary-foreground: var(--green);
--secondary: var(--canvas);
--secondary-foreground: var(--ink-secondary);
--destructive: var(--red);
--destructive-foreground: var(--white);
--radius: 5px;
```

### Tailwind bridge

Extend `tailwind.config` `theme.extend.colors` so Fieldstone variables are also accessible directly: `bg-canvas`, `bg-paper`, `bg-ink`, `text-ink`, `text-ink-muted`, `border-border-strong`, `bg-green-faint`, `text-orange`, etc. The shadcn semantic colors continue to work via the existing config.

`fontFamily` extension:
- `font-display` → Libre Baskerville
- `font-sans` (default) → IBM Plex Sans
- `font-mono` → IBM Plex Mono

### Fonts

Add to `index.html`:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@300;400;500;600&family=IBM+Plex+Mono:wght@400;500&family=Libre+Baskerville:wght@400;700&display=swap" rel="stylesheet">
```

`font-display: swap` accepted: Libre Baskerville is visually distinct and will pop in; the swap is brief and acceptable.

### Conventions update

Replace the line in `.claude/rules/frontend/react-conventions.md` that reads "shadcn components are owned source files (not a node_module) — edit them freely" with a new section:

> ## Theming shadcn components
>
> shadcn UI primitives in `src/web/src/components/ui/` are vendored but treated as **read-only**. Theme them by updating CSS variables in `src/web/src/index.css` (`--background`, `--primary`, `--border`, `--radius`, etc.) — never by editing variant classes in the component files.
>
> When a design needs something the stock variants cannot express:
> - **New visual variant of an existing primitive** (e.g. a "warn" button) → create a wrapper component in `components/ui/` that composes the primitive with extra classes. Don't add variants to the primitive itself.
> - **New domain component** (e.g. `StatusBadge`, `StatusChip`) → new file in `components/ui/`, may compose shadcn primitives internally.
>
> Why: keeping primitives stock means upstream shadcn updates apply with `pnpm dlx shadcn add --overwrite`, no merge conflicts. Forks accumulate drift; wrappers and tokens don't.

## Section 2 — AppShell

`OperatorLayout`, `AdminLayout`, and `WaitlistShellLayout` collapse onto one shared `AppShell` component. The shell has two variants:

### Variants

- **`variant="full"`** — sidebar + topbar + content + optional right rail. Used by operator and admin (today and after migration).
- **`variant="minimal"`** — topbar + content + optional right rail. No sidebar. Used by `WaitlistShellLayout` for phase-1 walkup-only operators.

Both variants share every primitive (Topbar, RightRail, content padding), so the brand reads as one product whether or not the sidebar is shown.

### Component tree

```
<AppShell variant="full" | "minimal" navConfig={...}>
  <Outlet />
</AppShell>
```

Pages contribute topbar and right-rail content from inside `<Outlet>` via the slot mechanism described below.

### Regions

1. **`<Sidebar>`** (full only) — `bg-ink`, 228px fixed.
   - `<SidebarLogo>` — Libre Baskerville wordmark + uppercase tracking subtitle
   - **No course selector.** The mockup's course card slot is dropped; sidebar goes straight from logo to nav.
   - `<SidebarNav sections={navConfig.sections}>` — grouped nav items with section labels, icons, optional badges, active indicator (3px green left bar via `::before`)
   - `<SidebarUser>` — avatar + name + role footer
2. **`<Topbar>`** (both variants) — 56px white bar with three slots: left, middle, right. Page-level content is provided via a `<PageTopbar>` component (see Page slot mechanism below) that fills named slots from inside the page render tree.
3. **`<MainContent>`** — `bg-paper` scroll region. `<Outlet />` renders here. Provides default page padding so individual pages don't manage their own outermost wrapper.
4. **`<RightRail>`** (both variants, optional) — 272px white panel, `bg-white`, `border-l border-border`. **Page-state-driven, not user-toggled.** Renders only when a page provides content via the slot mechanism (see below). When the slot content changes between null and non-null, the region appears or disappears in-place. Main content reflows wider when the rail is absent (push, not overlay). No enter/exit animation; pages that want one wrap their own panel content. AppShell holds zero state related to the rail.

### Page slot mechanism

Because pages render inside `<Outlet />`, they can't pass props to the surrounding `AppShell`. Instead, AppShell provides four slots — `topbarLeft`, `topbarMiddle`, `topbarRight`, and `rightRail` — that pages fill from inside their own render tree using small portal-based helpers:

- **`<PageTopbar>`** — a component with `left`, `middle`, `right` props (each accepting a ReactNode). Internally uses `createPortal` to render the children into the corresponding regions of `AppShell`'s topbar.
- **`<PageRightRail>`** — same pattern, single `children` prop, portals into the AppShell right rail region. When no page renders `<PageRightRail>`, the rail region is absent from the DOM and main content takes full width. To "open" the rail, a page renders `<PageRightRail>{content}</PageRightRail>`; to "close" it, the page conditionally renders nothing.

AppShell exposes the portal targets via React context so the helpers can find them without prop drilling. Pages import `<PageTopbar>` and `<PageRightRail>` from `@/components/layout`. The contract is purely declarative: a page that wants a topbar renders `<PageTopbar>`, a page that wants a rail renders `<PageRightRail>` — no imperative open/close calls.

### Nav configs (colocated with features)

- `src/web/src/features/operator/navigation.ts` exports `operatorNav`
- `src/web/src/features/admin/navigation.ts` exports `adminNav`

`AppShell` itself is nav-agnostic. The `navConfig` shape is `{ sections: { label: string; items: NavItem[] }[] }` where `NavItem` is `{ to: string; label: string; icon: ReactNode; badge?: string | number }`.

`operatorNav` initial sections: Operations (Tee Sheet, Waitlist, No-Shows), Management (Tee Time Settings, Pricing), Analytics (Fill Rate, Revenue). Items that point to routes that don't exist yet are still listed if they exist in the current `OperatorLayout` nav today; nothing new is invented.

`adminNav` initial sections: Platform (Dashboard, Orgs, Courses, Users), System (Feature Flags, Dead Letters). Same rule — only items that exist today.

### Layout shims (preserve routing)

- **`OperatorLayout.tsx`** rewritten to ~5 lines: `<AppShell variant="full" navConfig={operatorNav}><Outlet /></AppShell>`. Marked with a comment flagging removal in Cluster 4.
- **`AdminLayout.tsx`** rewritten the same way with `adminNav`. Marked for removal in Cluster 2.
- **`WaitlistShellLayout.tsx`** rewritten to use `<AppShell variant="minimal">`. Topbar slots: left = current `displayName` logic (course?.name ?? user?.organization?.name ?? 'Teeforce'), middle = empty, right = `<UserMenu onSwitchCourse={...} />` with the existing `showSwitchCourse` and `handleSwitchCourse` logic preserved exactly. Functional behavior 100% identical to today.

## Section 3 — Base components & wrappers

### shadcn primitives

**Untouched.** No source edits to any file in `src/web/src/components/ui/`. The retheme happens entirely through CSS variables and the Tailwind bridge. This means `<Button>`, `<Card>`, `<Input>`, `<Dialog>`, `<Table>`, `<Badge>`, `<Tabs>`, `<Sheet>`, `<DropdownMenu>`, `<Form>`, `<Tooltip>`, etc. all become Fieldstone-styled with zero diffs to their files. Future shadcn upstream updates apply cleanly.

### New primitives

- **`StatusBadge`** — `src/web/src/components/ui/status-badge.tsx`. Wraps shadcn `<Badge>` with a `status` prop (`booked` | `open` | `waitlist` | `checkedin` | `noshowed`) and applies the right Fieldstone classes via `className`. Stock `<Badge>` stays unmodified. Variants for statuses we don't currently produce (`waitlist`, `checkedin`, `noshowed`) are defined now and used later.
- **`StatusChip`** — `src/web/src/components/ui/status-chip.tsx`. The mockup's topbar dot-pill: small rounded pill with a colored dot, text label, and color family (`green` | `orange` | `gray`). Brand-new component, not derived from shadcn. Defined now, no instance rendered in this PR.
- **`PanelSection`** — `src/web/src/components/layout/PanelSection.tsx`. The right-rail section wrapper from the mockup: uppercase 11px tracked title with an optional inline link, padded body, bottom border between sections. Defined now for use by future cluster pages that populate the right rail.

### `PageHeader` restyle

Existing `src/web/src/components/layout/PageHeader.tsx` updated so the page title uses `font-display` (Libre Baskerville) — the only place in the app outside the sidebar logo that uses the serif. Body description stays in Plex Sans. No prop changes; pages that use it inherit the new look.

## Section 4 — TeeSheet proof page

The redesign of `src/web/src/features/operator/pages/TeeSheet.tsx` is the foundation PR's proof that the language works on a real operator page.

### Data reality

`useTeeSheet` returns slots shaped as `{ teeTime, status: 'booked' | 'open' | ..., golferName, playerCount }`. There is no per-player data. The mockup's "Player 1 / Player 2 / Player 3 / Player 4" columns are not backed by real data, so the redesigned row matches today's columns: **Time | Status | Golfer | Players**.

### Page structure

The page renders inside `OperatorLayout` (the AppShell shim) via `<Outlet>`. It does not wrap itself in `AppShell`. It contributes topbar content via `<PageTopbar>` and renders the grid as its main content:

```tsx
return (
  <>
    <PageTopbar
      left={<TeeSheetTopbarTitle courseName={data.courseName} selectedDate={selectedDate} />}
      right={<TeeSheetDateNav selectedDate={selectedDate} onDateChange={setSelectedDate} />}
    />
    <TeeSheetGrid slots={data.slots} now={now} />
  </>
);
```

The page no longer renders its own `<PageHeader>` — `<PageTopbar>` replaces it. The `<div className="p-6">` wrapper is gone; AppShell's content region handles padding. No `<PageRightRail>` is rendered, so the rail region stays absent.

### `TeeSheetTopbar`

Fills `<AppShell>`'s topbar slots:
- **Left slot:** course name (Plex Sans 15px semibold) over formatted date (Plex Sans 12px muted). Course name comes from `data.courseName`; date comes from `formatWallClockDate` of the first slot's `teeTime`, falling back to `selectedDate`.
- **Middle slot:** empty. No status chips (would require new aggregations).
- **Right slot:** `‹ [Today] ›` date nav.
  - `‹` and `›` step `selectedDate` ±1 day.
  - `[Today]` calls the existing course-local-today logic and resets `selectedDate` to it.
  - The native date picker remains accessible behind a small calendar icon button in the same group, so users can still jump to an arbitrary date.

### `TeeSheetGrid` and `TeeSheetRow`

`TeeSheetGrid`:
- Sticky header row with column labels (`Time`, `Status`, `Golfer`, `Players`) styled as 10px uppercase tracked muted text.
- Iterates `slots` and renders `TeeSheetRow` for each.
- Inserts a `<NowMarker>` element between the row whose `teeTime` is just before `now` and the row that is just after `now` — derived purely from `now` vs slot times, no new data.

`TeeSheetRow`:
- Single grid row with the four columns above plus row variants.
- **Time** cell: Plex Mono 12px, ink color (or muted for past).
- **Status** cell: `<StatusBadge status={mapTeeTimeStatus(slot.status)} />`. The mapper currently produces `booked` and `open`; other variants stay defined-but-unused.
- **Golfer** cell: `[avatar with initials from golferName][golferName]`. Initials derived in a small helper. When `golferName` is missing/empty (status `open`), renders an `—` placeholder. No handicap subtitle. No "waitlist fill" subtitle.
- **Players** cell: Plex Mono with `slot.playerCount`, or `—` when status is `open`.
- **Row variants** (derived from `now`, no new data):
  - `past` — `bg-canvas` for slots whose `teeTime` is before `now`.
  - `current-time` — `bg-white` with a green left bar (`box-shadow: inset 3px 0 0 var(--green)`) for the slot whose `teeTime` is the next future or in-progress slot.
  - default — `bg-paper`.

`PlayerCell`, `Avatar` (initials variant), `EmptySlot`, and `NowMarker` are small leaf components in `features/operator/components/`. They are page-internal helpers, not shared primitives.

### State and behavior preserved

- Same `useTeeSheet(courseId, selectedDate)` hook, same query key, same loading/error/empty handling.
- `if (!course)` "Select a course" message — kept, restyled with new tokens.
- `not configured` error → "Configure your tee times to get started" + "Go to Settings" CTA — kept, restyled.
- Generic error → red text — kept, restyled.
- No new endpoints, no new fields.

### Right rail

Tee sheet does not render `<PageRightRail>`. The rail region is absent. Main content takes full width.

## Section 5 — Migration plan (follow-up clusters)

Each cluster ships as a single PR. Cluster issues are filed as sub-issues of a parent epic "Operator/Admin redesign rollout" and linked from the foundation PR description.

### Cluster 1 — Phase-1 product (highest priority)

**Files:** `WaitlistShellLayout.tsx` (already shimmed in foundation, fully restyled here), `WalkUpWaitlist.tsx`

The `WaitlistShellLayout` shim from the foundation PR is replaced by a proper restyled implementation: minimal AppShell variant with the topbar (brand left, user menu right), no sidebar, no right rail. `WalkUpWaitlist` is restyled to live inside it cleanly.

Highest priority because the walkup waitlist is Teeforce's phase-1 product — the most-used surface for current customers.

### Cluster 2 — Admin CRUD

**Files:** `OrgList`, `OrgDetail`, `OrgCreate`, `CourseList`, `CourseDetail`, `CourseCreate`, `UserList`, `UserDetail`, `UserCreate`

Nine pages, three patterns (list / detail / create). One PR. Patterns get migrated together so restyling one List restyles all three Lists. The `AdminLayout` shim is fully removed in this PR — routes move to `<AppShell variant="full" navConfig={adminNav}>` directly.

### Cluster 3 — Admin system

**Files:** `Dashboard.tsx`, `FeatureFlags.tsx`, `DeadLetters.tsx`

Admin power tools, low traffic. Mostly tables and toggles — should fall out of stock components after Cluster 2. Lowest admin priority.

### Cluster 4 — Operator long-tail

**Files:** `TeeSheet.tsx` (already done in foundation — listed for completeness), `CoursePortfolio.tsx`, `OrgPicker.tsx`, `TeeTimeSettings.tsx`

Lowest urgency: portfolio/picker pages are navigated through occasionally, settings is rarely touched. The `OperatorLayout` shim is fully removed in this PR.

### Ordering rationale

Phase-1 product (1) → admin platform tools (2, 3) → operator long-tail (4). Admin comes before operator long-tail because the admin CRUD pages share heavily with each other, so doing them as a block while the patterns are fresh is more efficient than splitting them.

### Cluster issue template

Each cluster gets a GitHub issue with:
- **Pages list** (which files)
- **Done criteria:** every listed page renders in `<AppShell>` directly (no layout wrapper); page-level outermost wrapper removed; any custom inline colors replaced with tokens; visual QA screenshot for each page
- **Out of scope:** new functionality, new endpoints, new aggregations
- **Tracking parent:** the "Operator/Admin redesign rollout" epic

## Section 6 — Testing, done criteria, risks

### Testing

**Unit tests:** No new unit tests. Existing tests must continue to pass. Tests that locate elements removed by the redesign (e.g. the `<PageHeader title="Tee Sheet">` text or the date `<Input>`) get updated to find the new topbar elements instead. Tests that assert on data behavior (slot count, status badge text, "Configure your tee times" empty state) **stay unchanged** — those assertions are specifications and the behavior didn't change.

**E2E tests:** Audit `e2e/` for tee-sheet specs and locator fixtures. Update locators where the redesign removes a semantic anchor:
- The current `<Input type="date">` becomes a `‹ [Today] ›` button group.
- The `<h2>` "courseName - date" moves into the topbar.
- Add `data-testid` to new structural elements only where Playwright cannot find them by role: `data-testid="tee-sheet-grid"`, `data-testid="topbar-date-nav"`, `data-testid="now-marker"`.

Run the full e2e suite locally before opening the PR to catch surprise breakages from token shifts on pages that weren't intentionally redesigned.

**Manual smoke (`make dev`):** required by project rule. Click through the operator tee sheet, walkup waitlist routes, one admin page, and one golfer page before declaring done.

### Done criteria

- [ ] Tokens defined in `index.css`, mapped to shadcn semantic tokens
- [ ] Fonts loaded via `index.html` with preconnect
- [ ] `tailwind.config` extended with Fieldstone colors and font families
- [ ] `AppShell` implemented with both `full` and `minimal` variants
- [ ] `OperatorLayout` rewritten as a wrapper around `<AppShell variant="full" navConfig={operatorNav}>`
- [ ] `AdminLayout` rewritten as a wrapper around `<AppShell variant="full" navConfig={adminNav}>`
- [ ] `WaitlistShellLayout` rewritten as a wrapper around `<AppShell variant="minimal">` with current `displayName` and `UserMenu` behavior preserved
- [ ] `operatorNav` colocated in `features/operator/navigation.ts`
- [ ] `adminNav` colocated in `features/admin/navigation.ts`
- [ ] `StatusBadge`, `StatusChip`, `PanelSection` exist
- [ ] `PageHeader` restyled to use `font-display`
- [ ] `TeeSheet` page redesigned: topbar, mono time cells, restyled status badges, now marker, current/past row treatments, all original data and behavior preserved
- [ ] `react-conventions.md` updated: "edit them freely" line removed, theming convention added
- [ ] All existing unit tests passing (with locator updates where forced)
- [ ] All e2e tests passing (with locator updates where forced)
- [ ] `pnpm --dir src/web lint` clean
- [ ] `pnpm --dir src/web test` clean
- [ ] Manual smoke via `make dev`: tee sheet, walkup waitlist, one admin page, one golfer page render without visual disasters
- [ ] PR description includes before/after screenshots: tee sheet, walkup waitlist, one admin list page, one golfer page
- [ ] Cluster 1–4 follow-up issues filed under the "Operator/Admin redesign rollout" epic and linked from the PR description

### Risks

1. **Token cascade breaks pages we didn't redesign.** Pages with hardcoded color classes (`bg-blue-500`, `text-red-700`, etc.) may fight the new tokens. **Mitigation:** grep `features/` for hardcoded color classes before opening the PR; convert obvious offenders or accept them as cluster work.
2. **Golfer pages inherit changes we don't intend.** Walkup, golfer flows, etc. all use the same shadcn primitives. **Mitigation:** visual QA; if anything is actively broken (not just "different"), pin with explicit overrides on those routes — don't let perfect-redesign block foundation merge.
3. **Font load FOUC.** Libre Baskerville is visibly different from a sans fallback. **Mitigation:** `font-display: swap` + preconnect; accept brief swap on first load.
4. **`useTeeSheet` doesn't return enough for nice avatars.** Already addressed: initials derive from `golferName`; no avatar when name is missing.
5. **Conventions update may surprise other agents mid-flight.** **Mitigation:** PR description calls out the convention change explicitly.
6. **Layout shims leave dead code temporarily.** `OperatorLayout` and `AdminLayout` remain as ~5-line wrappers until clusters 2 and 4 complete. Acceptable transitional state; flagged for removal in their respective cluster issues.
