# Admin System Redesign — Cluster 3

**Date:** 2026-04-08
**Branch:** `chore/fieldstone-cluster-3-admin-system`
**Tracking:** #384 (sub-issue of #381 — Operator/Admin redesign rollout)
**Foundation:** PR #380, [`docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`](2026-04-06-operator-admin-redesign-foundation-design.md)
**Precedent:** Cluster 2 PR (admin CRUD), [`docs/superpowers/specs/2026-04-08-admin-crud-cluster-2-design.md`](2026-04-08-admin-crud-cluster-2-design.md)

## Summary

Cluster 3 of the Fieldstone redesign rollout. Restyle and restructure the three admin "system" pages — `Dashboard`, `FeatureFlags`, `DeadLetters` — to use the Fieldstone design language inside the existing full-variant `<AppShell>` mount that Cluster 2 wired up. These pages are admin power-tools: low traffic, mostly tables / charts / toggles, and they fall almost entirely out of the patterns Cluster 2 established.

The pages' data hooks, mutations, behaviors, and routing are unchanged. Only layout and visual treatment change. No new endpoints, no new aggregations, no new fields, no new dialogs, no new actions, no new unit or e2e tests.

## Out of scope

- Any change to `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, or any foundation primitive.
- Any change to `app/router.tsx`. Cluster 2 already inlined the `<AppShell>` mount around all admin routes.
- Any change to `features/admin/navigation.tsx` or `features/admin/components/StatTile.tsx` / `DetailTitle.tsx`. These are reused as-is.
- Any change to `OperatorLayout.tsx`, `WaitlistShellLayout.tsx`, or `GolferLayout.tsx`.
- New product features (no new endpoints, no new fields, no new aggregations, no new actions, no new dialogs).
- New unit or e2e tests. Existing tests stay; locators are updated only where the redesign forces them.
- Mobile / small-screen layout. Desktop-first; below ~1024px panels may squish but are not designed for.
- New foundation tokens. Cluster 2's shadow tokens already cover the Card surface; no chart-specific tokens are introduced.
- The right rail (`<PageRightRail>`) — none of the three pages render it.
- Cluster 4 (`TeeSheet`, `CoursePortfolio`, `OrgPicker`, `TeeTimeSettings`) and the `OperatorLayout` shim removal.

## Section 1 — Architecture (none)

Nothing structural changes. There is no shell to delete, no router to rewire, no helper to extract. Cluster 2 already deleted `AdminLayout.tsx`, renamed `navigation.ts → navigation.tsx`, and inlined `<AppShell variant="full" navConfig={adminNav} brand={adminBrand}>` around all `/admin/*` routes. Cluster 3 inherits that mount unchanged.

The three pages each contribute their title via `<PageTopbar>` from inside the render tree, render no in-body header row, and drop their outer `p-6 space-y-6` wrapper. AppShell content padding takes over.

`StatTile` (Cluster 2) is reused by `Dashboard`. `DetailTitle` (Cluster 2) is not used by any of the three pages — none have a back-chevron pattern.

## Section 2 — Dashboard

### Topbar

```tsx
<PageTopbar
  middle={<h1 className="font-display text-[18px] text-ink">Analytics Dashboard</h1>}
/>
```

- **Middle slot:** the page title in display font at 18 px (the topbar size, not the in-body 24 px size).
- **Right slot:** unused. Dashboard has no primary action.
- **Left slot:** unused — the sidebar brand anchors the left.
- **Body header row deleted** — the in-body `<h1 className="text-2xl font-bold">Analytics Dashboard</h1>` is gone.

### Body structure

After the topbar contribution, the body is one column of regions. No outer wrapper div. Four regions, in order:

1. **Summary tiles row** — 4 `<StatTile>` instances (Total Organizations, Total Courses, Active Users, Bookings Today).
2. **Fill Rates chart panel** — full width.
3. **Booking Trends + Popular Times** — two-column grid of two chart panels.
4. **Waitlist Stats panel** — a single `<Card border-border-strong>` wrapping a 4-tile `<StatTile>` grid.

### Summary tiles row

```tsx
<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
  <StatTile
    label="Total Organizations"
    value={
      summary.isLoading
        ? <Skeleton className="h-7 w-12 inline-block" />
        : (summary.data?.totalOrganizations ?? '—')
    }
  />
  {/* Total Courses, Active Users, Bookings Today — same pattern */}
</div>
```

- **Local `StatCard` helper deleted.** All 8 stat positions on the page (4 here + 4 in Waitlist Stats below) use the shared `StatTile` from `features/admin/components/StatTile.tsx`.
- **Loading state** passes a `<Skeleton>` as the `value` prop. `StatTile`'s `value: ReactNode` signature handles it natively — same pattern Cluster 2 uses.
- **Missing data** renders as the em-dash literal `'—'`, identical to today.

### `ChartPanel` local helper

The existing `ChartCard` page-local helper is renamed to `ChartPanel` and restyled to match the Cluster 2 panel idiom:

```tsx
function ChartPanel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}
```

- **Page-local, not exported.** It's used three times on the same file; promoting it to `features/admin/components/` would be premature.
- **Title strings unchanged** verbatim (`"Fill Rates (Last 7 Days)"`, `"Booking Trends (Last 30 Days)"`, `"Popular Times"`). The uppercase className handles the visual transform.
- **No `<CardContent>` padding override.** Charts already manage their own internal padding via `ResponsiveContainer`.

### Chart palette sweep

Three recharts hex literals → Fieldstone tokens, applied in place:

| Chart | Element | Today | After |
|---|---|---|---|
| Fill Rates | `<Line stroke=...>` | `#2563eb` | `var(--green)` |
| Booking Trends | `<Line stroke=...>` | `#16a34a` | `var(--green)` |
| Popular Times | `<Bar fill=...>` | `#9333ea` | `var(--ink)` |

No other recharts props change. `CartesianGrid`, `XAxis`, `YAxis`, `Tooltip` retain their current props; `text-muted-foreground` references inside `EmptyChart` already resolve to `--ink-muted` via the cascade.

If a chart renders the wrong color in practice (recharts internals occasionally read computed colors imperatively for legend swatches and tooltip dots), the fallback is to inline the token's resolved hex value for that one prop and document the exception in the PR description.

### Waitlist Stats panel

Today the waitlist row sits below a free-floating `<h2>Waitlist Stats</h2>`. After:

```tsx
<Card className="border-border-strong">
  <CardHeader>
    <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
      Waitlist Stats
    </CardTitle>
  </CardHeader>
  <CardContent>
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <StatTile label="Active Entries" value={...} />
      <StatTile label="Offers Sent" value={...} />
      <StatTile label="Offers Accepted" value={...} />
      <StatTile label="Offers Rejected" value={...} />
    </div>
  </CardContent>
</Card>
```

- **Free-floating `<h2>` deleted.** The grouping moves into the panel header.
- **Same `StatTile` loading pattern** as the top row.
- **Visual rhythm note:** the top-row tiles sit on canvas, the bottom-row tiles sit inside a bordered panel. This is intentional — the panel-wrap is the grouping cue that replaces the deleted heading.

### Loading and empty states

- **Stat tiles:** loading state inline via `<Skeleton>` as the `value` prop.
- **Charts:** existing `<Skeleton className="h-[300px] w-full" />` inside `<ChartPanel>` is unchanged.
- **`EmptyChart` component:** unchanged. Its `text-muted-foreground` class resolves to `--ink-muted` via the cascade.

### What gets removed from `Dashboard.tsx`

- The local `StatCard` function component (8 call sites migrate to `StatTile`).
- The in-body `<h1 className="text-2xl font-bold">Analytics Dashboard</h1>`.
- The free-floating `<h2 className="text-lg font-semibold mb-3">Waitlist Stats</h2>` and its containing `<div>`.
- The hex chart colors `#2563eb`, `#16a34a`, `#9333ea`.
- The outer `<div className="space-y-6 p-6">` wrapper.

### What stays identical

- All five `useAnalytics` hook calls and their data shapes (`summary`, `fillRates`, `bookingTrends`, `popularTimes`, `waitlistStats`).
- Recharts component tree (`LineChart`, `BarChart`, `ResponsiveContainer`, `CartesianGrid`, `XAxis`, `YAxis`, `Tooltip`, `Line`, `Bar`).
- All chart titles, axis units, tooltip formatters, `dataKey` props.
- The `EmptyChart` and `StatCard`-replaced-by-`StatTile` loading conditionals.

## Section 3 — FeatureFlags

### Topbar

```tsx
<PageTopbar
  middle={<h1 className="font-display text-[18px] text-ink">Feature Flags</h1>}
/>
```

- **Middle slot:** title.
- **Right slot:** unused.
- **Body header row + subtitle deleted** — the in-body `<h1>Feature Flags</h1>` and the muted `<p>Manage feature availability per organization</p>` are gone.

### Body

After the topbar contribution, the body is one panel — same idiom as Cluster 2's Detail/Create form Cards:

```tsx
<Card className="border-border-strong">
  <CardHeader>
    <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
      Organization Features
    </CardTitle>
  </CardHeader>
  <CardContent className="p-0">
    {/* table or empty / loading / error state */}
  </CardContent>
</Card>
```

`CardContent className="p-0"` lets the table run to the panel edges (the same pattern Cluster 2 uses for the embedded tables inside `OrgDetail`'s tabs).

### Feature label humanization

A page-local lookup, no logic at the call site:

```tsx
const FEATURE_LABELS: Record<FeatureKey, string> = {
  'sms-notifications': 'SMS Notifications',
  'dynamic-pricing': 'Dynamic Pricing',
  'full-operator-app': 'Full Operator App',
};
```

Used only for the visible column header text:

```tsx
<TableHead key={key} className="text-[10px] uppercase tracking-wider text-ink-muted whitespace-nowrap">
  {FEATURE_LABELS[key]}
</TableHead>
```

The kebab keys still drive everything else: the `FEATURE_KEYS` const, the `FeatureKey` type, the `Record<FeatureKey, boolean>` data shape, the `aria-label` on `<Switch>` (`${key} for ${org.name}`), and the `setOrgFeatures.mutate({ orgId, flags })` payload. Nothing in the data or interaction layer changes.

### Table restyle

- **Outer wrapper:** none. `<CardContent className="p-0">` is the wrapper.
- **`<TableHeader>` row:** add `bg-canvas`, each `<TableHead>` gets `text-[10px] uppercase tracking-wider text-ink-muted`.
- **First column (org name) cell:** `font-medium` (already there), no other change.
- **Switch cells:** untouched at the call site. `<Switch>` inherits Fieldstone via the cascade — track green when `checked`, canvas when not.

The today-existing `<div className="overflow-x-auto">` wrapper inside `<CardContent>` is dropped — the Card panel handles the visible bounds.

### Loading / empty / error states

All three states render as a single centered paragraph inside the Card body — same idiom as the Cluster 2 List page empty states:

- **Loading** (`isLoading`): `<p className="text-ink-muted text-sm py-12 text-center">Loading organizations...</p>`
- **Empty** (`!organizations || organizations.length === 0`): `<p className="text-ink-muted text-sm py-12 text-center">No organizations found.</p>`
- **Error** (`error`): `<p className="text-destructive text-sm py-12 text-center">Error: {error instanceof Error ? error.message : 'Failed to load organizations'}</p>`

All copy is preserved verbatim from today; only the wrapping element changes.

### What gets removed from `FeatureFlags.tsx`

- The in-body `<h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Feature Flags</h1>` and its `<div>` wrapper.
- The subtitle `<p className="text-sm text-muted-foreground">Manage feature availability per organization</p>`.
- The early-return `<div className="p-6">` wrappers around the loading and error states.
- The outer `<div className="p-6 space-y-6">` wrapper.
- The `<div className="overflow-x-auto">` table wrapper inside `<CardContent>`.

### What stays identical

- `useOrganizations` hook call.
- `useSetOrgFeatures` mutation hook call.
- The local `useState<OrgFlags>` flags-state pattern, `getFlag`, and `handleToggle`.
- The `FEATURE_KEYS` constant and the `FeatureKey` type.
- All `aria-label` attributes on `<Switch>` (test selectors).
- Mutation payload shape `{ orgId, flags }`.

## Section 4 — DeadLetters

### Topbar

```tsx
<PageTopbar
  middle={
    <h1 className="font-display text-[18px] text-ink">
      Dead Letter Queue
      {page && page.totalCount > 0 && (
        <span className="ml-2 font-mono text-[13px] text-ink-muted">· {page.totalCount}</span>
      )}
    </h1>
  }
  right={
    selectedIds.size > 0 ? (
      <div className="flex items-center gap-2">
        <span className="text-[12px] text-ink-muted">{selectedIds.size} selected</span>
        <Button variant="outline" size="sm" onClick={handleReplay} disabled={replay.isPending}>
          {replay.isPending ? 'Replaying…' : 'Replay'}
        </Button>
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="destructive" size="sm" disabled={deleteMessages.isPending}>
              {deleteMessages.isPending ? 'Deleting…' : 'Delete'}
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete dead letter messages?</AlertDialogTitle>
              <AlertDialogDescription>
                This will permanently delete {selectedIds.size}{' '}
                {selectedIds.size === 1 ? 'message' : 'messages'}. This action cannot be undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={handleDelete}>Delete</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    ) : null
  }
/>
```

- **Middle slot:** title plus an inline mono count suffix `· {totalCount}` that renders only when `totalCount > 0`. The empty-state title reads simply `Dead Letter Queue`.
- **Right slot:** the bulk-action cluster. Renders only when `selectedIds.size > 0`. Empty otherwise.
- **`AlertDialog` body unchanged** — copy, structure, and handlers verbatim. Only the trigger's parent moves from the body header into the topbar right slot.
- **Body header row deleted** — the entire `<div className="flex items-center justify-between">…</div>` block is gone.

### Body

After the topbar contribution, the body is the table (or the empty state). No outer wrapper div.

### Empty state

```tsx
<p className="text-ink-muted text-sm py-12 text-center">No dead letter messages. All clear.</p>
```

The bordered box wrapper is dropped — same idiom as Cluster 2 list-page empty states.

### Table restyle

Per Cluster 2's table rules, applied identically:

- **Outer wrapper:** `border border-border-strong rounded-md bg-white overflow-hidden` (replaces today's `border rounded-md`).
- **`<TableHeader>` row:** add `bg-canvas`; each `<TableHead>` gets `text-[10px] uppercase tracking-wider text-ink-muted`.
- **Checkbox column:** unchanged at the call site; visual via cascade.
- **Message Type cell:** keep `font-medium font-mono`, replace `text-sm` with `text-[13px] text-ink`.
- **Exception Type cell:** `text-[13px] text-destructive` (was `text-sm text-destructive` — token already correct, just align font size).
- **Exception Message cell:** `font-mono text-[12px] text-ink-muted` (was `text-sm text-muted-foreground`).
- **Sent At cell:** `font-mono text-[12px] text-ink-muted` (was `text-sm text-muted-foreground whitespace-nowrap`; keep `whitespace-nowrap`).
- **Row** `cursor-pointer` and click-to-expand handler unchanged.

The `formatSentAt`, `stripNamespace`, and `truncate` helpers are unchanged.

### `ExpandedRow` restyle

```tsx
function ExpandedRow({ envelope }: ExpandedRowProps) {
  return (
    <TableRow>
      <TableCell colSpan={5} className="bg-canvas px-6 py-4">
        <div className="space-y-3" data-testid="dead-letter-detail">
          <div>
            <p className="text-[11px] uppercase tracking-wider text-ink-muted mb-1">Exception Message</p>
            <p className="text-sm whitespace-pre-wrap break-words text-ink">{envelope.exceptionMessage}</p>
          </div>
          <div>
            <p className="text-[11px] uppercase tracking-wider text-ink-muted mb-1">Message Body</p>
            <pre className="font-mono text-[12px] bg-canvas border border-border-strong rounded-md p-3 overflow-x-auto whitespace-pre-wrap break-words">
              {JSON.stringify(envelope.message, null, 2)}
            </pre>
          </div>
        </div>
      </TableCell>
    </TableRow>
  );
}
```

- **Row background:** `bg-muted/40` → `bg-canvas` (token-aligned, near-identical visual).
- **Section labels:** `text-xs font-semibold uppercase text-muted-foreground` → `text-[11px] uppercase tracking-wider text-ink-muted` (matches the rest of Cluster 2/3's section headers).
- **JSON `<pre>`:** `bg-background border` → `bg-canvas border border-border-strong`; add explicit `font-mono text-[12px]`.
- **`data-testid="dead-letter-detail"` preserved verbatim.**

### Loading and error states

The early-return loading and error states are kept structurally but tokenized:

- **Loading:** `<p className="text-ink-muted">Loading dead letter messages...</p>` inside a topbar-contributing wrapper. To keep behavior simple, the early returns can stay as page-level `<div className="py-12 text-center"><p ...>…</p></div>` with no outer `p-6`. The topbar still renders via the portal because `<PageTopbar>` is a sibling at the top of the JSX tree in each branch — or, more simply, the loading/error states render after the `<PageTopbar>` tag at the top of the component's return.
- **Error:** same pattern, `text-destructive` token unchanged.

### What gets removed from `DeadLetters.tsx`

- The in-body `<div className="flex items-center justify-between">…</div>` header row, including:
  - The `<h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Dead Letter Queue</h1>`
  - The subtitle `<p>{page ? \`${page.totalCount} failed messages\` : 'Messages that failed processing'}</p>`
  - The right-side selection-count + Replay + Delete cluster
- The `<div className="border rounded-md p-12 text-center">` empty-state box.
- The early-return `<div className="p-6">` wrappers around loading and error states.
- The outer `<div className="p-6 space-y-6">` wrapper.

### What stays identical

- All three hooks: `useDeadLetters`, `useReplayDeadLetters`, `useDeleteDeadLetters`.
- All local state: `selectedIds`, `expandedIds`.
- All handlers: `handleSelectAll`, `handleSelectOne`, `handleToggleExpand`, `handleReplay`, `handleDelete`.
- All helpers: `stripNamespace`, `truncate`, `formatSentAt`.
- The full `<AlertDialog>` body, including the destructive-confirmation copy and the singular/plural switch.
- All `aria-label` attributes on Checkboxes.
- The `data-testid="dead-letter-detail"` attribute.
- The `toLocaleString` Sent At format.

## Section 5 — Files, tests, rollout

### Files modified

```
src/web/src/features/admin/pages/Dashboard.tsx
  - Delete local StatCard helper; replace 8 call sites with <StatTile>
  - Rename ChartCard → ChartPanel (page-local); restyle <Card> + <CardTitle> to Fieldstone idiom
  - Contribute title via <PageTopbar>; drop in-body <h1>
  - Wrap Waitlist Stats tiles in a <Card border-border-strong> panel; delete free-floating <h2>
  - Convert recharts hex colors to Fieldstone tokens (var(--green), var(--ink))
  - Drop outer <div className="space-y-6 p-6">

src/web/src/features/admin/pages/FeatureFlags.tsx
  - Contribute title via <PageTopbar>; drop in-body <h1> and subtitle
  - Add FEATURE_LABELS map for humanized column headers (kebab keys preserved everywhere else)
  - Wrap body in <Card border-border-strong> with restyled <CardTitle>; <CardContent className="p-0">
  - Apply table restyle (uppercase tracked headers, tokenized cells)
  - Move loading/error/empty states inside the Card; align to Fieldstone tokens
  - Drop outer <div className="p-6 space-y-6"> and the <div className="overflow-x-auto"> table wrapper

src/web/src/features/admin/pages/DeadLetters.tsx
  - Contribute title + conditional · count via <PageTopbar middle>
  - Portal bulk-action cluster (Replay + Delete + AlertDialog) into <PageTopbar right> when selection exists
  - Drop in-body header row entirely
  - Apply table restyle (uppercase headers, mono cells, tokenized colors)
  - Restyle ExpandedRow (bg-canvas, tracked labels, tokenized <pre>)
  - Flatten empty state to centered muted paragraph
  - Tokenize loading/error early-returns
  - Drop outer <div className="p-6 space-y-6"> and the empty-state border box
```

### Files created

None.

### Files deleted

None.

### Files NOT touched

- `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx` — foundation primitives
- `OperatorLayout.tsx`, `WaitlistShellLayout.tsx`, `GolferLayout.tsx` — Cluster 4 / already shipped
- `app/router.tsx`, `features/admin/navigation.tsx`, `features/admin/index.tsx` — Cluster 2 territory
- `features/admin/components/StatTile.tsx`, `DetailTitle.tsx` — reused as-is
- All shadcn primitives in `src/web/src/components/ui/` (read-only per convention)
- `features/admin/hooks/useAnalytics.ts`, `useFeatureFlags.ts`, `useDeadLetters.ts` — data layer unchanged
- `index.css` — no new tokens
- Backend code, types, query keys

### Tests

Per the cluster's "no new tests" rule, zero new `.test.tsx` or `.spec.ts` files are created. Existing tests are touched **only** when their locators break.

**Likely locator impact (per page):**

- **`Dashboard.test.tsx`** (if it exists): tests querying the page heading by role for `"Analytics Dashboard"` continue to work (the `<h1>` lives in the topbar portal but renders in the DOM tree). Tests querying `"Waitlist Stats"` by `getByRole('heading', …)` break — the `<h2>` is gone; the text now lives inside a `<CardTitle>` styled as uppercase tracked muted. Update to `getByText(/Waitlist Stats/i)` or, preferably, query the panel by its containing test id.
- **`FeatureFlags.test.tsx`** (if it exists): tests querying column headers by `getByRole('columnheader', { name: /sms-notifications/i })` break because the humanized label is now `"SMS Notifications"`. Update the matcher to the humanized form. The `aria-label` on `<Switch>` is unchanged (`${key} for ${org.name}`), so switch-interaction tests are unaffected.
- **`DeadLetters.test.tsx`** (if it exists): tests that find the Replay and Delete buttons by role still work (the buttons exist, just portaled into the topbar). Tests that scope queries to a specific in-body header container break. The `data-testid="dead-letter-detail"` attribute on the expanded row is preserved verbatim, so detail-row assertions are unaffected.

**E2E tests:** audit `e2e/` for admin specs. Likely-affected anchors: the Feature Flags column headers now read humanized; the DeadLetters bulk-action buttons now render in the topbar region rather than the body header. Run affected specs locally before opening the PR.

### Manual smoke (per project rule)

After implementation, run `make dev` and click through:

- `/admin/dashboard` — verify topbar title, 4 summary tiles render with mono numbers, three chart panels render with green/ink colors, Waitlist Stats panel wraps the 4 lower tiles with an uppercase header
- `/admin/feature-flags` — verify topbar title, table headers read `SMS Notifications` / `Dynamic Pricing` / `Full Operator App`, switches toggle and persist, loading/empty states render inside the Card
- `/admin/dead-letters` — verify topbar title, count suffix appears only when `totalCount > 0`, clicking rows expands them, selecting rows populates the topbar-right action cluster, Replay still calls the API, Delete still triggers the AlertDialog, AlertDialog still confirms before delete, empty state reads cleanly when no messages
- One operator page (e.g. tee sheet) — sanity check that the foundation surface still works
- One golfer page (e.g. walkup join) — sanity check that no shared token cascade has shifted

Plus:

- `pnpm --dir src/web lint` clean
- `pnpm --dir src/web test` clean (no new tests; locator updates only where forced)

### Rollout

One PR titled `feat(web): Fieldstone redesign — admin system (Cluster 3)`. Body uses `Closes #384` so the cluster sub-issue closes on merge and the parent epic #381 reflects the progress. Includes before/after screenshots of all three pages (Dashboard is the largest visual shift due to the chart palette change and the panel-wrapped Waitlist Stats; FeatureFlags and DeadLetters are smaller).

## Section 6 — Risks

1. **Recharts color tokens via CSS variables.** Recharts accepts `stroke="var(--green)"` at render time, but some recharts internals (legend swatches, tooltip dots) may read computed colors imperatively and cache them. **Mitigation:** test manually under `make dev`. If a chart renders the wrong color anywhere, fall back to inlining the token's resolved hex value for that one prop and document the exception in the PR description.
2. **Waitlist Stats panel-wrap changes the visual rhythm of the Dashboard.** Today all eight stat tiles read as one flat space divided only by a heading. After, the top 4 tiles sit on canvas and the bottom 4 sit inside a bordered card — slightly different visual weight. **Mitigation:** accept it; the grouping is the point.
3. **DeadLetters topbar-right is selection-conditional.** The `PageTopbar right` slot goes from empty to populated as the user selects rows. The portal re-creates on every selection change because the JSX passed to `right` changes identity. This is the same pattern Cluster 1 used for `WalkUpWaitlist`'s conditional topbar actions, so it's proven. **Mitigation:** none required; flagged for awareness.
4. **Feature label humanization is display-only.** If any test or URL param depends on the column header text matching the kebab key, it breaks. **Mitigation:** grep before writing; update matchers if needed. The data layer, the `aria-label`s, and the mutation payload all keep the kebab keys.
5. **`<pre>` JSON block restyle.** The `<pre>` currently uses `text-xs` (12px). The restyle specifies `font-mono text-[12px]` — same visual size, just explicit. **Mitigation:** none; documented as a no-op.
6. **`AlertDialog` portaled inside a portaled topbar slot.** The `<AlertDialog>` component already manages its own portal to `document.body`, so nesting it inside a portaled topbar slot is safe. The dialog renders at the top level of the DOM, not inside the topbar region.

## Done criteria

- [ ] `Dashboard.tsx` uses `<StatTile>` for all 8 stat positions; the local `StatCard` helper is deleted
- [ ] All three Dashboard charts use Fieldstone tokens (`var(--green)`, `var(--ink)`) instead of hex literals
- [ ] Dashboard Waitlist Stats tiles are wrapped in a `<Card border-border-strong>` panel with an uppercase tracked `<CardTitle>`
- [ ] Dashboard `ChartCard` is renamed to `ChartPanel` and restyled to the Fieldstone panel idiom
- [ ] `FeatureFlags.tsx` renders a `FEATURE_LABELS` humanized header row while keeping kebab keys as the data layer, mutation payload, and `aria-label` source
- [ ] `FeatureFlags.tsx` body is wrapped in a `<Card border-border-strong>` with a restyled `<CardTitle>` and `<CardContent className="p-0">`
- [ ] `DeadLetters.tsx` portals the bulk-action cluster (count + Replay + Delete + AlertDialog) into `<PageTopbar right>` conditionally on selection
- [ ] `DeadLetters.tsx` topbar middle renders the `· {totalCount}` count suffix only when `totalCount > 0`
- [ ] `DeadLetters.tsx` `ExpandedRow` is restyled (`bg-canvas`, tracked labels, tokenized `<pre>`)
- [ ] All three pages contribute their title via `<PageTopbar>` and render no in-body header row or subtitle
- [ ] All three pages drop their outer `p-6 space-y-6` wrapper
- [ ] All tables use the restyled idiom (uppercase tracked headers, mono cells, `border-border-strong` wrapper where applicable)
- [ ] Hardcoded color classes (`bg-muted/40`, `bg-background`, `text-muted-foreground` where appropriate) converted to Fieldstone tokens
- [ ] No source edits to any file in `src/web/src/components/ui/`
- [ ] No edits to `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, `app/router.tsx`, `features/admin/navigation.tsx`, `StatTile.tsx`, or `DetailTitle.tsx`
- [ ] `pnpm --dir src/web lint` clean
- [ ] `pnpm --dir src/web test` clean (no new tests; locator updates only where forced)
- [ ] Manual smoke (`make dev`) green for all three pages + one operator page + one golfer page
- [ ] PR opened with `Closes #384`, before/after screenshots of all three pages, link to this spec
