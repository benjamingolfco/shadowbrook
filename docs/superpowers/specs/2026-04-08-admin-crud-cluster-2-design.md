# Admin CRUD Redesign — Cluster 2

**Date:** 2026-04-08
**Branch:** `chore/fieldstone-cluster-2-admin-crud`
**Tracking:** #383 (sub-issue of #381 — Operator/Admin redesign rollout)
**Foundation:** PR #380, [`docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`](2026-04-06-operator-admin-redesign-foundation-design.md)
**Precedent:** Cluster 1 PR #386, [`docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md`](2026-04-07-walkup-waitlist-cluster-1-design.md)

## Summary

Cluster 2 of the Fieldstone redesign rollout. Restyle and restructure all nine admin CRUD pages — three patterns (List / Detail / Create) across three entities (Org / Course / User) — to use the Fieldstone design language inside the existing full-variant `AppShell`. Migrating the patterns as a block while they're fresh is more efficient than splitting them across PRs and produces a single consistent admin surface.

The `AdminLayout` shim that the foundation PR introduced is **fully removed** in this PR. Admin routes mount `<AppShell>` directly from the router, with `adminBrand` colocated alongside `adminNav` in the admin feature module. Pages contribute their identity via `<PageTopbar>` slot helpers — the topbar carries the page title and the primary action; the body opens straight into content with no in-body header row.

The pages' data, hooks, mutations, schemas, and routing are unchanged. Only layout and visual treatment change. No new endpoints, no new aggregations, no new fields, no new dialogs, no new actions, no new unit or e2e tests.

## Out of scope

- Any change to `OperatorLayout.tsx`, `WaitlistShellLayout.tsx`, `AppShell.tsx`, or any foundation primitive.
- The right rail. None of the nine pages render `<PageRightRail>`.
- Mobile / small-screen layout. Desktop-first; below ~1024px the topbar and tabs may squish but are not designed for. Deferred until usage data shows it matters.
- New product features (no new fields, no new actions, no new dialogs, no new endpoints, no new hooks, no new mutations).
- New unit or e2e tests. Existing tests stay; locators are updated only where the redesign forces them.
- URL state for the `OrgDetail` tabs. The default tab is Details and refreshing always lands there.
- Cluster 4's pages (`TeeSheet`, `CoursePortfolio`, `OrgPicker`, `TeeTimeSettings`) and the `OperatorLayout` shim removal — those land in their own cluster.
- `Dashboard.tsx`, `FeatureFlags.tsx`, `DeadLetters.tsx` — those are Cluster 3.

## Section 1 — Architecture & shell removal

### `AdminLayout.tsx` is deleted

The 5-line shim from the foundation PR is removed. Admin routes mount `<AppShell>` directly. In `src/web/src/app/router.tsx`:

```tsx
import { Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { adminNav, adminBrand } from '@/features/admin/navigation';

<Route
  element={
    <AppShell variant="full" navConfig={adminNav} brand={adminBrand}>
      <Outlet />
    </AppShell>
  }
>
  <Route path="/admin" ...>
  {/* all child admin routes unchanged */}
</Route>
```

`AdminBrand` (the tiny presentational component inside the deleted `AdminLayout.tsx`) ceases to exist. Its replacement lives next to `adminNav`.

### `features/admin/navigation.ts` → `navigation.tsx`

The file is renamed via `git mv` so JSX is allowed, and a sibling export is added:

```tsx
import type { ReactNode } from 'react';

export const adminNav = { /* unchanged */ };

export const adminBrand: ReactNode = (
  <span className="font-display text-lg text-sidebar-foreground">Teeforce</span>
);
```

The router imports `{ adminNav, adminBrand }` from one module. No new file is created in `components/layout/`; the brand is admin-specific (the operator brand decision — possibly the course name — belongs in Cluster 4).

### Layouts NOT touched

- `OperatorLayout.tsx` — Cluster 4
- `WaitlistShellLayout.tsx` — already shimmed in foundation, used by `CoursePortfolio` (Cluster 4) and the walk-up waitlist (already shipped)
- `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx` — foundation primitives, frozen here

## Section 2 — Topbar pattern

Each page contributes via `<PageTopbar>` from inside its render tree. `<PageRightRail>` is never rendered — the rail region stays absent on every admin page.

The topbar is 56 px tall. The page title uses `font-display` (Libre Baskerville) at **18 px**, not the in-body 24 px size, so it sits comfortably with the sidebar brand on the left.

### List pages (`OrgList`, `CourseList`, `UserList`)

```tsx
<PageTopbar
  middle={<h1 className="font-display text-[18px] text-ink">Organizations</h1>}
  right={
    <Button asChild>
      <Link to="/admin/organizations/new">Create Organization</Link>
    </Button>
  }
/>
```

- **Middle slot:** the page title in display font. The only place on the page that uses the serif.
- **Right slot:** the primary "Create X" button. Inherits Fieldstone primary styling (green-faint background, green text) automatically via the foundation token cascade.
- **Left slot:** unused — the sidebar brand already anchors the left.
- **Body header row deleted** — no in-body `<h1>`, no in-body Create button.

### Detail pages (`OrgDetail`, `CourseDetail`, `UserDetail`)

```tsx
<PageTopbar
  middle={<DetailTitle backTo="/admin/organizations" title={org?.name} />}
/>
```

`DetailTitle` is a small page-internal helper (see Section 4) that renders a back-chevron link plus the entity name in display font. Loading state shows a `<Skeleton>` in place of the title; the page-type fallback (`'Organization'`) renders if the query errors before any data arrives.

- **Middle slot:** small back-chevron + display-font entity name.
- **Right slot:** unused for now. When entity-level destructive actions are added later, they go in the topbar right as an overflow `<DropdownMenu>`, mirroring Cluster 1's pattern.
- **Body header row deleted** — no in-body Back button, no in-body `<h1>`.

### Create pages (`OrgCreate`, `CourseCreate`, `UserCreate`)

```tsx
<PageTopbar
  middle={<DetailTitle backTo="/admin/organizations" title="Create Organization" />}
/>
```

Same `DetailTitle` helper, called with a literal title string instead of an entity name. No skeleton renders (we always pass a value).

- **Middle slot:** back-chevron + literal `"Create X"` title.
- **Right slot:** unused. Submit and Cancel are form-scoped, not page-scoped, so they stay inside the form Card.
- **Body header row deleted** — no in-body Back button, no in-body `<h1>`.

## Section 3 — List pattern

After the topbar contribution, the body of every list page renders just two things: a **summary tiles row** and the **table**. No outer `p-6` div — AppShell content padding handles it. No `space-y-6` wrapping div.

### Summary tiles row

A grid of three Fieldstone "stat tiles" above the table, replacing today's three shadcn `<Card>` summary blocks. Each tile uses the new page-internal `<StatTile>` helper:

```tsx
<div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
  <StatTile label="Total Organizations" value={orgs?.length ?? 0} />
  <StatTile label="Total Courses" value={totalCourses} />
  <StatTile label="Total Users" value={totalUsers} />
</div>
```

`StatTile` lives in `src/web/src/features/admin/components/StatTile.tsx`. It composes shadcn `<Card>` (per the convention "new domain components may compose shadcn primitives internally") with explicit Fieldstone treatment:

```tsx
import type { ReactNode } from 'react';
import { Card } from '@/components/ui/card';

export function StatTile({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Card className="border-border-strong p-4">
      <p className="text-[11px] uppercase tracking-wider text-ink-muted">{label}</p>
      <p className="mt-1 font-mono text-[28px] text-ink leading-none">{value}</p>
    </Card>
  );
}
```

- **Domain helper, not a primitive.** Lives under `features/admin/components/`, not `components/ui/`. Reused by all three List pages.
- **`p-4` on `<Card>` directly**, no `<CardHeader>`/`<CardContent>` — the stock subcomponents have `p-6` defaults that waste space for a two-line tile.
- **`border-border-strong`** so the tile reads as a defined panel rather than a wash on the canvas.
- **No `shadow-none` override.** `<Card>`'s built-in `shadow-sm` is now token-driven (see Section 6 for the foundation extension), so we leave the className alone and let the token decide.
- **`value` accepts `ReactNode`** so the loading state can pass `<Skeleton className="h-7 w-12 inline-block" />` directly without restructuring the tile.

### Table restyle (no source edits)

Stay on shadcn `<Table>`. All restyles are call-site class additions:

- **Outer wrapper:** `border border-border-strong rounded-md bg-white overflow-hidden` (replaces today's `border rounded-md`).
- **`<TableHeader>` row:** add `bg-canvas` and apply `text-[10px] uppercase tracking-wider text-ink-muted` to each `<TableHead>`. Same column-header idiom as TeeSheet and `OpeningsGrid`.
- **`<TableRow>`:** clickable rows keep `cursor-pointer`. Hover background already resolves to `--muted` (= `--canvas`) via the foundation cascade — no class change needed.
- **First-column cells (entity name):** keep `font-medium`, drop `font-semibold` if present. Names sit better next to mono numerics when subdued.
- **Numeric cells (counts):** add `font-mono text-[13px] text-ink`. Today they're plain `<TableCell>` text.
- **Date cells:** add `font-mono text-[12px] text-ink-muted`. Replace existing `text-sm` with the mono class. Date format unchanged (`toLocaleDateString()`).
- **Status `<Badge>`:** untouched at the call site. The badge inherits Fieldstone via the cascade — `default` becomes green-faint/green, `secondary` becomes canvas/ink-muted.

### Loading / empty / error states

- **Loading:** existing skeleton table renders the same way. Row skeletons inherit the new column-header treatment because they share `<TableHead>`.
- **Empty:** existing copy ("No organizations yet. Create one to get started.") restyled as `text-ink-muted text-sm py-12 text-center`. No card chrome.
- **Error:** existing inline error + Retry button. `text-destructive` is already a token; the Retry `<Button variant="outline" size="sm">` already inherits the cascade. No structural change.

### Per-page differences

- **`OrgList`:** Name / Courses / Users / Created. Tiles: Total Organizations / Total Courses / Total Users.
- **`CourseList`:** ports its existing columns and tile set 1-for-1. Implementation reads the current file once, applies the table restyle rules, and replaces the existing summary cards with `<StatTile>` instances using the same labels/values.
- **`UserList`:** same approach.

No new columns. No new tiles. No new aggregations.

## Section 4 — Detail pattern

After the topbar contribution, the body of every Detail page renders a single column of content. No outer wrapper div. Content widths are managed per-region (`max-w-2xl` for single-pane forms, `max-w-4xl` for the tabbed `OrgDetail`).

### `DetailTitle` helper

Lives in `src/web/src/features/admin/components/DetailTitle.tsx`. Used by all three Detail pages and all three Create pages (six callers):

```tsx
import { Link } from 'react-router';
import { ChevronLeft } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';

export function DetailTitle({ backTo, title }: { backTo: string; title?: string }) {
  return (
    <div className="flex items-center gap-3">
      <Link
        to={backTo}
        className="text-ink-muted hover:text-ink"
        aria-label="Back"
      >
        <ChevronLeft className="h-4 w-4" />
      </Link>
      <h1 className="font-display text-[18px] text-ink">
        {title ?? <Skeleton className="h-5 w-40 inline-block" />}
      </h1>
    </div>
  );
}
```

- **`aria-label="Back"`** on the chevron link so locator-based tests that look up the back link by accessible name continue to work even though the visible "Back" text is gone.
- **Loading state** shows a skeleton in place of the title; tests that wait for the title text don't false-negative during the brief load.
- **Page-internal helper**, not a primitive. Lives under `features/admin/components/`.

### `CourseDetail` and `UserDetail` (single-pane)

Per the design discussion, these have no tabs — there's only one section. Body is just a Fieldstone form panel:

```tsx
<>
  <PageTopbar middle={<DetailTitle backTo="/admin/courses" title={course?.name} />} />

  <div className="max-w-2xl">
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          Details
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          {/* existing form fields, validation, mutation, success/error states — unchanged */}
        </Form>
      </CardContent>
    </Card>
  </div>
</>
```

- **`<CardTitle>` restyled** to the uppercase 11 px tracked muted idiom — same as TeeSheet section headers and `OpeningsGrid`'s "TODAY'S OPENINGS" line. Override is at the call site (`className="text-[11px] uppercase tracking-wider text-ink-muted font-normal"`), not on the primitive.
- **`max-w-2xl` wrapper** keeps form columns readable on wide screens.
- **Existing form behavior preserved verbatim** — `useForm`, Zod schema, mutation, success/error states, Save button. Only the visual chrome around the form changes.
- **Hardcoded color sweep:** the existing `text-green-600` success message in `OrgDetail` is converted to `text-green` (the Fieldstone token). One-liner per file.

### `OrgDetail` (tabbed)

Per the design discussion, `OrgDetail` is the only Detail page with tabs because it's the only one with multiple sections (Details / Courses / Users). Tabs use shadcn `<Tabs>` (themed via cascade) and live in the page body just under the topbar:

```tsx
<>
  <PageTopbar middle={<DetailTitle backTo="/admin/organizations" title={org?.name} />} />

  <Tabs defaultValue="details" className="max-w-4xl">
    <TabsList>
      <TabsTrigger value="details">Details</TabsTrigger>
      <TabsTrigger value="courses">Courses</TabsTrigger>
      <TabsTrigger value="users">Users</TabsTrigger>
    </TabsList>

    <TabsContent value="details">
      <Card className="border-border-strong">
        <CardHeader>
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Details
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Form {...form}>{/* unchanged */}</Form>
        </CardContent>
      </Card>
    </TabsContent>

    <TabsContent value="courses">
      <Card className="border-border-strong">
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Courses
          </CardTitle>
          <Button variant="outline" size="sm" asChild>
            <Link to={`/admin/courses/new?organizationId=${id}`}>Register Course</Link>
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {/* existing courses table, restyled per Section 3 table rules */}
        </CardContent>
      </Card>
    </TabsContent>

    <TabsContent value="users">
      <Card className="border-border-strong">
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Users
          </CardTitle>
          <Button variant="outline" size="sm" asChild>
            <Link to={`/admin/users/new?organizationId=${id}`}>Create User</Link>
          </Button>
        </CardHeader>
        <CardContent className="p-0">
          {/* existing users table, restyled */}
        </CardContent>
      </Card>
    </TabsContent>
  </Tabs>
</>
```

Notes:

1. **`defaultValue="details"`**, uncontrolled. No URL state, no router integration. Refresh always lands on Details. Deferred to a future improvement.
2. **Tab labels are sentence case**, no counts. Counts already live on the list page tiles.
3. **The "Register Course" / "Create User" action buttons** stay tab-scoped (inside each `CardHeader`), not page-scoped. They keep their existing `?organizationId=<id>` query string behavior.
4. **The embedded `<Table>` inside each tab** restyles per the Section 3 rules. One pass, applied identically.
5. **`max-w-4xl`** instead of `max-w-3xl` because the tabbed content needs to accommodate the embedded tables.

### Loading and error states

- **Loading:** `<TabsContent>` panels render their own skeletons (form skeleton inside Details, table skeletons inside Courses/Users). Tabs themselves render immediately.
- **Error:** existing inline `<p className="text-destructive">` renders above the tabs, same as today's behavior. Wrapping it in a `<div className="max-w-4xl">` keeps it visually aligned with the tabs container.

### What gets removed from each Detail page

- The in-body `<div className="flex items-center gap-4">` with the Back button + `<h1>` (relocated to topbar via `DetailTitle`).
- The `font-[family-name:var(--font-heading)]` inline style on the `<h1>` (relocated to `DetailTitle`'s `font-display` class).
- For `OrgDetail`: the three-Card stack in the body (replaced by the tabbed layout).
- The `text-green-600` hardcoded color on success messages (converted to `text-green`).
- The outer `<div className="p-6 space-y-6 max-w-3xl">` wrapper (AppShell content padding takes over).

## Section 5 — Create pattern

After the topbar contribution, the body of every Create page is one Fieldstone form panel — the same idiom as the Detail page's Details panel:

```tsx
<>
  <PageTopbar middle={<DetailTitle backTo="/admin/organizations" title="Create Organization" />} />

  <div className="max-w-2xl">
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          Organization Details
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            {/* existing fields — unchanged */}

            {createMutation.isError && (
              <p className="text-sm text-destructive">{/* existing message */}</p>
            )}

            <div className="flex gap-3">
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Creating...' : 'Create Organization'}
              </Button>
              <Button type="button" variant="outline" asChild>
                <Link to="/admin/organizations">Cancel</Link>
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  </div>
</>
```

Notes:

1. **`DetailTitle` is reused** with a literal title string. The `title?: string` signature handles it; no skeleton renders because we always pass a value.
2. **`max-w-2xl`** matches Detail. Today's Create pages use `max-w-lg` which becomes too narrow once the topbar takes over the title.
3. **`<CardHeader>` + `<CardTitle>` restyled** to the uppercase 11 px tracked muted idiom. The text varies per page ("Organization Details" / "Course Details" / "User Details") — kept verbatim from today.
4. **Submit + Cancel buttons** stay inside the form, at the bottom. Form-scoped, not page-scoped. Submit is primary `<Button>` (Fieldstone green-faint via cascade). Cancel is `<Button variant="outline">`.
5. **Existing form behavior preserved verbatim** — `useForm`, Zod schema, mutation, navigation on success, error messages. The Send Invite checkbox in `OrgCreate`, the Org dropdown / picker in `CourseCreate` and `UserCreate`, all field-level UX — unchanged.
6. **Hardcoded color sweep** per file (e.g. `text-green-600 → text-green`). No structural changes.
7. **`gap-4 → gap-3`** on the button row, purely for tighter Fieldstone rhythm.

### What gets removed from each Create page

- The in-body `<div className="mb-6 flex items-center gap-4">` with the Back button + `<h1>` (relocated to topbar via `DetailTitle`).
- The `font-[family-name:var(--font-heading)]` inline style.
- The `max-w-lg` constraint on the outer wrapper (replaced by `max-w-2xl`).
- The outer `<div className="p-6">` wrapper (AppShell content padding takes over).

### What stays identical across all Create pages

- **Schemas, validation, default values, mutations, navigation on success** — every Create page's `useForm` / `zodResolver` / `useCreateX` / `onSubmit` block is unchanged.
- **The exact field set per page** — no new fields, no removed fields.
- **Error messages and success behavior** — all inline strings preserved verbatim.

## Section 6 — Foundation extension: shadow tokens

Cluster 2 is the first cluster to exercise shadcn `<Card>` panels at scale, so it's the natural place to introduce shadow tokens. The foundation spec was written assuming Tailwind v3 syntax (`tailwind.config.js`), but the actual implementation landed on Tailwind v4 with `@theme inline` in `index.css`. This cluster lands two new tokens at **stock Tailwind values** so that future clusters can flip Fieldstone shadows in one place.

In `src/web/src/index.css`, inside the existing `@theme inline` block:

```css
@theme inline {
  /* ...existing color, font, radius tokens... */

  --shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.05);
  --shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
}
```

Two effects:

1. **Visible result is identical** — Card / popover / dialog / dropdown all look exactly the same. No visual change in this PR. The values are the literal stock Tailwind defaults.
2. **Single source of truth** — when Fieldstone's paper-flat treatment is applied to small panels, changing those two lines flips every Card in the app at once. Overlays (`shadow-md` / `lg` / `xl` / `2xl`) are deliberately left alone so popovers and dialogs keep their visual lift.

The `StatTile` and the Detail/Create form Cards therefore need **no `shadow-none` override** at the call site. The foundation token decides.

The PR description calls this out explicitly as a Cluster 2 foundation extension.

## Section 7 — Files, tests, and rollout

### Files created

```
src/web/src/features/admin/components/
├── DetailTitle.tsx              # Topbar middle helper: <-back chevron + display-font title
└── StatTile.tsx                 # Composes <Card>; uppercase tracked label + mono numeric value
```

Both are page-internal helpers in `features/admin/components/`. Neither is a primitive; neither lives in `components/ui/`. `StatTile` is reused by all three List pages; `DetailTitle` is reused by all three Detail pages and all three Create pages (six callers).

### Files modified

```
src/web/src/index.css
  - Add --shadow-sm and --shadow tokens inside @theme inline (stock Tailwind values; no visual change)

src/web/src/features/admin/navigation.ts → navigation.tsx
  - Rename via git mv so JSX is allowed
  - Add `adminBrand` ReactNode export

src/web/src/app/router.tsx
  - Replace <Route element={<AdminLayout />}> with inline AppShell mount
  - Import { adminNav, adminBrand } from features/admin/navigation
  - All /admin/* child routes unchanged

src/web/src/features/admin/pages/OrgList.tsx
  - Drop in-body header + Create button row (relocated to PageTopbar)
  - Replace 3-card summary block with 3 <StatTile> instances
  - Apply table restyle rules
  - Drop outer <div className="p-6 space-y-6"> wrapper

src/web/src/features/admin/pages/CourseList.tsx
  - Same treatment as OrgList

src/web/src/features/admin/pages/UserList.tsx
  - Same treatment

src/web/src/features/admin/pages/OrgDetail.tsx
  - Drop in-body Back button + <h1> row (relocated)
  - Replace stacked Cards with shadcn <Tabs>: Details / Courses / Users
  - Each tab content is a <Card border-border-strong> with restyled <CardTitle>
  - Restyle the embedded Courses and Users tables
  - Convert text-green-600 → text-green
  - Drop outer wrapper (replaced by max-w-4xl on <Tabs>)

src/web/src/features/admin/pages/CourseDetail.tsx
  - Drop in-body Back + <h1> row
  - Wrap Details form in <Card border-border-strong> with restyled <CardTitle>
  - Convert any hardcoded color classes to Fieldstone tokens
  - Drop outer wrapper

src/web/src/features/admin/pages/UserDetail.tsx
  - Same treatment

src/web/src/features/admin/pages/OrgCreate.tsx
  - Drop in-body Back + <h1> row
  - Restyle <CardTitle>
  - max-w-lg → max-w-2xl
  - Convert hardcoded color classes
  - Drop outer p-6 wrapper

src/web/src/features/admin/pages/CourseCreate.tsx
  - Same treatment

src/web/src/features/admin/pages/UserCreate.tsx
  - Same treatment
```

### Files deleted

```
src/web/src/components/layout/AdminLayout.tsx
```

### Files NOT touched

- `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx` — foundation primitives
- `OperatorLayout.tsx`, `WaitlistShellLayout.tsx` — Cluster 4 / Cluster 1 territory
- `WalkUpWaitlist.tsx`, `TeeSheet.tsx`, `OpeningsGrid.tsx`, etc. — already shipped
- All shadcn primitives in `src/web/src/components/ui/` — read-only per convention
- `StatusBadge.tsx`, `StatusChip.tsx`, `PanelSection.tsx` — foundation wrappers, no extension needed
- `useOrganizations`, `useCourses`, `useUsers` and the API client — no data layer changes
- Backend code, types, query keys — untouched
- `features/admin/index.tsx` — barrel re-exports unchanged

### Tests

Per the cluster's "no new tests" rule, no new `.test.tsx` or `.spec.ts` files are created. Existing tests are touched **only** when their locators break.

**Tests likely to need locator updates:**

- `src/web/src/features/admin/__tests__/*.test.tsx`:
  - `getByRole('heading', { name: /Organizations/i })` continues to work — the `<h1>` lives in the topbar via portal, but it's still in the rendered DOM tree.
  - `getByRole('link', { name: /Create Organization/i })` continues to work for the same reason.
  - Locators that query the back link by visible "Back" text break (we removed the visible text). The chevron link has `aria-label="Back"`, so updating to `getByRole('link', { name: /Back/i })` or `getByLabelText(/Back/i)` keeps the test green.
  - For `OrgDetail.test.tsx` (if it exists): tests that assert Courses or Users table content render must first click the corresponding tab via `userEvent.click(screen.getByRole('tab', { name: /Courses/i }))`. **Behavior assertions stay** — what gets rendered inside a tab is unchanged.
  - Hardcoded color assertions (rare): if any test asserts on `text-green-600` className, swap to `text-green`. Far more likely there are zero such tests.
  - Tests that mount admin pages with a `<MemoryRouter>` and an explicit `<AdminLayout>` wrapper need updating to mount `<AppShell>` directly (or to skip the wrapper). The `AdminLayout` import disappears.

**E2E tests:** audit `e2e/` for admin specs. Likely-affected anchors: in-body `<h1>` (still findable by role), in-body Back button text (now icon-only, queryable via `aria-label="Back"`), in-body Create button (still findable by role), stat card text (now inside `StatTile`, same text content). Run admin e2e specs locally before opening the PR.

### Manual smoke (per project rule)

After implementation, run `make dev` and click through:

- `/admin/organizations` — verify topbar title + Create button, three stat tiles render with mono numbers, table renders with uppercase headers + mono dates, row click navigates to detail
- `/admin/organizations/new` — verify topbar title + back chevron, form Card with section header, submit creates an org and redirects
- `/admin/organizations/<id>` — verify topbar title shows the org name, three tabs render, switching tabs changes content, Save updates the name, Register Course and Create User buttons still navigate correctly with `?organizationId=<id>` query
- `/admin/courses` — verify list page renders cleanly
- `/admin/courses/new` — verify create page renders cleanly
- `/admin/courses/<id>` — verify detail page renders cleanly (single-pane, no tabs)
- `/admin/users` — list, `/admin/users/new` — create, `/admin/users/<id>` — detail
- One operator page (e.g. tee sheet) — sanity check that the foundation surface still works
- One golfer page (e.g. walkup join) — sanity check that the new shadow tokens (at stock values) didn't move anything

Plus:

- `pnpm --dir src/web lint` clean
- `pnpm --dir src/web test` clean (existing tests pass; locator updates only where forced)

### Rollout

One PR titled `feat(web): Fieldstone redesign — admin CRUD (Cluster 2)`. Body uses `Closes #383` so the cluster sub-issue closes on merge and the parent epic #381 reflects the progress. Includes before/after screenshots of:

- One List page (e.g. `OrgList`)
- One Detail page (e.g. `OrgDetail`, showing the tabs)
- One Create page (e.g. `OrgCreate`)

Plus an explicit callout that the PR introduces `--shadow-sm` / `--shadow` foundation tokens at stock values, so future clusters can flip Fieldstone shadows in one place.

## Section 8 — Risks

1. **Tab adoption in `OrgDetail` is the largest behavioral change in the cluster.** Today the page is a single scroll with all three sections visible at once; after this PR, two of the three sections live behind a click. **Mitigation:** tabs use shadcn's accessible `<Tabs>` primitive (keyboard navigable, aria-managed); the default tab is Details (the most common entry point); deep-link state is deferred (acceptable — same as today, refresh always lands on the form).
2. **Locator churn in admin tests.** Less risky than Cluster 1's `WalkUpWaitlist.test.tsx` because admin test coverage is thinner, but the `AdminLayout` deletion changes the route element wrapper, which may surface in tests that mount `<MemoryRouter>` and expect a specific layout hierarchy. **Mitigation:** update affected tests in one pass; behavior assertions stay unchanged.
3. **`AdminLayout` shim deletion ripples.** If any other code imports `AdminLayout` (unlikely but possible), the build breaks. **Mitigation:** grep for `AdminLayout` before deleting; remove all references.
4. **Tailwind v4 shadow tokenization is a foundation extension.** The foundation spec was written assuming v3 syntax and is silent on `@theme` shadow tokens. Adding `--shadow-sm` and `--shadow` is technically scope creep into the foundation. **Mitigation:** PR description calls this out explicitly; the values are stock Tailwind defaults so the visual surface area is zero in this PR; the change is two lines.
5. **`max-w-lg → max-w-2xl` on Create pages may surprise reviewers.** It's a width change for forms that worked fine narrow. **Mitigation:** documented here as a deliberate Fieldstone rhythm choice; if it lands wrong visually, revert to `max-w-lg` per page in a fixup.
6. **`navigation.ts → navigation.tsx` rename.** Renames are sometimes tricky in git. **Mitigation:** do it as `git mv` so history follows.
7. **Hardcoded color sweep may miss instances.** A grep across the nine pages catches `text-green-600`, `text-amber-*`, `text-blue-*`, `text-red-*` etc. — convert to Fieldstone tokens during implementation, case-by-case.

## Done criteria

- [ ] `AdminLayout.tsx` deleted; router mounts `<AppShell>` directly
- [ ] `features/admin/navigation.tsx` exports `adminNav` and `adminBrand`
- [ ] `index.css` has `--shadow-sm` and `--shadow` tokens at stock Tailwind values inside `@theme inline`
- [ ] `StatTile.tsx` exists in `features/admin/components/` and is used by all three List pages
- [ ] `DetailTitle.tsx` exists in `features/admin/components/` and is used by all three Detail pages and all three Create pages
- [ ] All nine pages contribute their topbar via `<PageTopbar>` and render no in-body header row
- [ ] All nine pages drop their outer `p-6` wrapper
- [ ] `OrgDetail` renders three `<TabsContent>` panels (Details / Courses / Users) with `defaultValue="details"`
- [ ] `CourseDetail` and `UserDetail` render a single Details panel
- [ ] All List tables use the restyled `<Table>` (uppercase tracked headers, mono cells, `border-border-strong` wrapper)
- [ ] All hardcoded color classes (e.g. `text-green-600`) converted to Fieldstone tokens
- [ ] No source edits to any file in `src/web/src/components/ui/`
- [ ] `pnpm --dir src/web lint` clean
- [ ] `pnpm --dir src/web test` clean (no new tests; locator updates only where forced)
- [ ] Manual smoke (`make dev`) green for all nine pages + one operator page + one golfer page
- [ ] PR opened with `Closes #383`, before/after screenshots of one page from each pattern, link to this spec, and an explicit callout of the foundation shadow-token addition
