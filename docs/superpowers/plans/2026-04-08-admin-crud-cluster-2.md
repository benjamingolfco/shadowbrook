# Fieldstone Redesign — Admin CRUD (Cluster 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle all nine admin CRUD pages (Org / Course / User × List / Detail / Create) to the Fieldstone design language inside the full-variant `AppShell`, delete the `AdminLayout` shim, and land the foundation shadow tokens. Visual/structural only — zero new functionality, zero new tests.

**Architecture:** Admin routes mount `<AppShell>` directly from `features/admin/index.tsx` (deleting the 19-line `AdminLayout.tsx` shim). Each page contributes its topbar via `<PageTopbar>` slot helpers from inside its render tree; body opens straight into content with no in-body header row. Two page-internal helpers (`StatTile`, `DetailTitle`) live under `features/admin/components/` — they are domain-scoped, not foundation primitives. Tailwind v4 `--shadow-sm` / `--shadow` tokens land at stock values inside the existing `@theme inline` block in `index.css` so future clusters can flip Fieldstone shadows in one place.

**Tech Stack:** React 19, TypeScript, Vite, React Router v7, Tailwind v4 (`@theme inline` in `index.css`, no `tailwind.config.js`), shadcn/ui primitives (read-only), React Hook Form + Zod, Vitest + React Testing Library.

**Spec:** [`docs/superpowers/specs/2026-04-08-admin-crud-cluster-2-design.md`](../specs/2026-04-08-admin-crud-cluster-2-design.md)

**Tracking:** closes #383

---

## Hard rules (repeat in every subagent dispatch)

These override default behavior. Any subagent dispatched to execute this plan MUST be briefed with these rules verbatim — subagents do not inherit conversation context.

- **NO new unit tests.** Existing tests keep passing. Update locators only when the redesign forces it. Test assertions are protected specifications.
- **NO new e2e tests.** Same rule.
- **NO new functionality.** Visual / structural only. No new endpoints, no new aggregations, no new fields, no new dialogs, no new actions. No new tile labels, no new table columns, no new tab counts.
- **shadcn primitives in `src/web/src/components/ui/` are read-only.** Theme via CSS variables in `src/web/src/index.css` (Tailwind v4 `@theme inline`, NOT `tailwind.config.js`). New visual variants live in wrapper components.
- **No new primitives in `components/ui/`.** Cluster 2's helpers (`StatTile`, `DetailTitle`) are domain-scoped and live under `features/admin/components/`.
- **Right rail is out of scope.** Do not render `<PageRightRail>` from any admin page.
- **`OperatorLayout.tsx` and `WaitlistShellLayout.tsx` are not modified.** The only layout file touched is `AdminLayout.tsx`, which is deleted entirely.
- **`AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, and `PageRightRail.tsx` are foundation primitives — frozen.**
- **Preserve existing loading / error / empty UX per page.** `OrgList` uses skeletons; `CourseList` uses text (`"Loading courses..."`). Do NOT unify them — the existing tests depend on the current text states. Only restyle what already exists structurally.
- **Preserve dual-render mobile rows.** `CourseList` has `md:hidden` mobile variants of its table cells that existing tests depend on. Do not delete them.
- **Commit after each task passes lint + test.**

---

## File structure

### Files created

```
src/web/src/features/admin/components/
├── DetailTitle.tsx    # Back chevron + display-font title (6 callers: 3 Detail + 3 Create)
└── StatTile.tsx       # Composes <Card>; uppercase tracked label + mono numeric value (3 List callers)
```

### Files modified

```
src/web/src/index.css                                 (Task 1 — add shadow tokens)
src/web/src/features/admin/navigation.ts              (Task 2 — git mv → navigation.tsx + adminBrand export)
src/web/src/features/admin/index.tsx                  (Task 5 — mount AppShell directly)
src/web/src/features/admin/pages/OrgList.tsx          (Task 6)
src/web/src/features/admin/pages/CourseList.tsx       (Task 7)
src/web/src/features/admin/pages/UserList.tsx         (Task 8)
src/web/src/features/admin/pages/OrgCreate.tsx        (Task 9)
src/web/src/features/admin/pages/CourseCreate.tsx     (Task 10)
src/web/src/features/admin/pages/UserCreate.tsx       (Task 11)
src/web/src/features/admin/pages/CourseDetail.tsx     (Task 12)
src/web/src/features/admin/pages/UserDetail.tsx       (Task 13)
src/web/src/features/admin/pages/OrgDetail.tsx        (Task 14)
```

### Files deleted

```
src/web/src/components/layout/AdminLayout.tsx         (Task 5)
```

### Files NOT touched

- `src/web/src/components/layout/AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx` — foundation primitives
- `src/web/src/components/layout/OperatorLayout.tsx`, `WaitlistShellLayout.tsx`, `GolferLayout.tsx` — not Cluster 2 territory
- `src/web/src/components/ui/*` — read-only
- `src/web/src/features/admin/hooks/*` — no data layer changes
- `src/web/src/features/admin/pages/Dashboard.tsx`, `FeatureFlags.tsx`, `DeadLetters.tsx` — Cluster 3
- `src/web/src/app/router.tsx` — the admin layout mount lives inside `features/admin/index.tsx`, NOT in router.tsx. The spec's code example showing `router.tsx` is a mis-reference; the actual mount point is `features/admin/index.tsx:20`.
- Backend code

---

## Phase 1 — Foundation (Tasks 1–5)

This phase lays down everything the page tasks depend on: shadow tokens, the navigation brand, the two helper components, and the router/layout swap. Dispatch these as a single batch of tasks to one subagent — they are a cohesive unit.

### Task 1: Add `--shadow-sm` and `--shadow` tokens to `index.css`

**Files:**
- Modify: `src/web/src/index.css`

Locate the existing `@theme inline { ... }` block and add two shadow tokens at stock Tailwind v4 default values. Visual surface area is zero — Cards, popovers, and dialogs render identically. The only purpose is to land a single source of truth so future clusters can flip Fieldstone shadows in one place.

- [ ] **Step 1: Read `src/web/src/index.css` and locate the `@theme inline` block**

- [ ] **Step 2: Add the two shadow tokens inside the existing `@theme inline` block**

Add these two lines inside `@theme inline { ... }`, alongside the existing color/font/radius tokens. Keep alphabetical or grouped placement consistent with the existing file — put them near any other `--shadow-*` tokens if present, otherwise at the end of the block.

```css
  --shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.05);
  --shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
```

- [ ] **Step 3: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean

- [ ] **Step 4: Run tests**

Run: `pnpm --dir src/web test`
Expected: all pass (no behavioral change)

- [ ] **Step 5: Commit**

```bash
git add src/web/src/index.css
git commit -m "feat(web): add --shadow-sm and --shadow foundation tokens at stock values"
```

---

### Task 2: Rename `navigation.ts` → `navigation.tsx` and add `adminBrand` export

**Files:**
- Rename: `src/web/src/features/admin/navigation.ts` → `src/web/src/features/admin/navigation.tsx`
- Modify: (the renamed file, to add the `adminBrand` JSX export)

`AdminBrand` (the tiny presentational component currently inside `AdminLayout.tsx`) ceases to exist. Its replacement — a `ReactNode` literal called `adminBrand` — lives next to `adminNav` so the two are imported together in Task 5.

- [ ] **Step 1: Rename via `git mv` so history follows**

Run: `git mv src/web/src/features/admin/navigation.ts src/web/src/features/admin/navigation.tsx`

- [ ] **Step 2: Read the renamed file to confirm current contents**

The file currently contains:

```ts
import type { NavConfig } from '@/components/layout/AppShell';

export const adminNav: NavConfig = {
  sections: [
    {
      label: 'Platform',
      items: [
        { to: '/admin', label: 'Dashboard' },
        { to: '/admin/organizations', label: 'Organizations' },
        { to: '/admin/courses', label: 'Courses' },
        { to: '/admin/users', label: 'Users' },
      ],
    },
    {
      label: 'System',
      items: [
        { to: '/admin/feature-flags', label: 'Feature Flags' },
        { to: '/admin/dead-letters', label: 'Dead Letters' },
      ],
    },
  ],
};
```

- [ ] **Step 3: Add the `adminBrand` export**

Replace the file's contents with the following (keeps `adminNav` unchanged, adds the `ReactNode` import and the `adminBrand` export):

```tsx
import type { ReactNode } from 'react';
import type { NavConfig } from '@/components/layout/AppShell';

export const adminNav: NavConfig = {
  sections: [
    {
      label: 'Platform',
      items: [
        { to: '/admin', label: 'Dashboard' },
        { to: '/admin/organizations', label: 'Organizations' },
        { to: '/admin/courses', label: 'Courses' },
        { to: '/admin/users', label: 'Users' },
      ],
    },
    {
      label: 'System',
      items: [
        { to: '/admin/feature-flags', label: 'Feature Flags' },
        { to: '/admin/dead-letters', label: 'Dead Letters' },
      ],
    },
  ],
};

export const adminBrand: ReactNode = (
  <span className="font-display text-lg text-sidebar-foreground">Teeforce</span>
);
```

- [ ] **Step 4: Verify no other file imports from `navigation.ts` with the explicit `.ts` extension**

Run grep: pattern `navigation\.ts` inside `src/web/src`.
Expected: zero hits (imports use the extensionless `@/features/admin/navigation` path). If any `.ts` suffix import is found, strip the extension.

- [ ] **Step 5: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean (the new export is not yet imported; the renamed file still exports `adminNav`).

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/admin/navigation.tsx
git commit -m "refactor(web): rename admin navigation to tsx and add adminBrand export"
```

---

### Task 3: Create `StatTile.tsx` helper

**Files:**
- Create: `src/web/src/features/admin/components/StatTile.tsx`

Domain helper, not a primitive. Composes shadcn `<Card>` internally with explicit Fieldstone treatment: `border-border-strong`, direct `p-4` on the Card (skipping the stock `<CardHeader>` / `<CardContent>` which default to wasteful `p-6`), uppercase tracked 11 px label, mono 28 px value. The `value` prop accepts `ReactNode` so loading states can pass a `<Skeleton>` without restructuring the tile.

- [ ] **Step 1: Create the file**

Create `src/web/src/features/admin/components/StatTile.tsx` with:

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

- [ ] **Step 2: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean (unused export, will be wired up in Phase 2).

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/admin/components/StatTile.tsx
git commit -m "feat(web): add StatTile helper for admin list summary tiles"
```

---

### Task 4: Create `DetailTitle.tsx` helper

**Files:**
- Create: `src/web/src/features/admin/components/DetailTitle.tsx`

Used by all three Detail pages and all three Create pages (six callers). Renders a back-chevron link plus the display-font page title. The chevron carries `aria-label="Back"` so locator-based tests that look up the back link by accessible name continue to work even though the visible "Back" text is gone. When `title` is undefined (Detail pages during load), a `<Skeleton>` renders in its place.

- [ ] **Step 1: Create the file**

Create `src/web/src/features/admin/components/DetailTitle.tsx` with:

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

- [ ] **Step 2: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/admin/components/DetailTitle.tsx
git commit -m "feat(web): add DetailTitle helper for admin topbar back+title slot"
```

---

### Task 5: Delete `AdminLayout.tsx`; mount `<AppShell>` directly in `features/admin/index.tsx`

**Files:**
- Delete: `src/web/src/components/layout/AdminLayout.tsx`
- Modify: `src/web/src/features/admin/index.tsx`

> **Important note (divergence from spec):** the Cluster 2 spec's Section 1 shows the AppShell mount happening in `app/router.tsx`, but the actual mount point is `features/admin/index.tsx:20` where `AdminFeature` mounts `<Route element={<AdminLayout />}>`. The spec's intent is clear — wherever `AdminLayout` mounts today, replace it with an inline `<AppShell>` — so in practice Task 5 edits `features/admin/index.tsx`, not `app/router.tsx`.

The `AdminLayout` shim deletion ripples to any other code that imports it. Step 1 greps for references to catch them. There should be exactly one importer: `features/admin/index.tsx`.

- [ ] **Step 1: Grep for `AdminLayout` references**

Use Grep with pattern `AdminLayout` across `src/web/src`.
Expected hits: exactly one — `src/web/src/features/admin/index.tsx` (plus the file itself, which is about to be deleted). If any other importer exists, STOP and escalate — the plan assumes only one importer.

- [ ] **Step 2: Update `features/admin/index.tsx` to mount `<AppShell>` directly**

Replace the entire file with:

```tsx
import { Routes, Route, Navigate, Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { adminNav, adminBrand } from './navigation';
import Dashboard from './pages/Dashboard';
import OrgList from './pages/OrgList';
import OrgCreate from './pages/OrgCreate';
import OrgDetail from './pages/OrgDetail';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import CourseDetail from './pages/CourseDetail';
import UserList from './pages/UserList';
import UserCreate from './pages/UserCreate';
import UserDetail from './pages/UserDetail';
import FeatureFlags from './pages/FeatureFlags';
import DeadLetters from './pages/DeadLetters';
import DevSmsPage from '@/features/dev/pages/DevSmsPage';

function AdminShell() {
  return (
    <AppShell variant="full" navConfig={adminNav} brand={adminBrand}>
      <Outlet />
    </AppShell>
  );
}

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminShell />}>
        <Route index element={<Dashboard />} />
        <Route path="organizations" element={<OrgList />} />
        <Route path="organizations/new" element={<OrgCreate />} />
        <Route path="organizations/:id" element={<OrgDetail />} />
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="courses/:id" element={<CourseDetail />} />
        <Route path="users" element={<UserList />} />
        <Route path="users/new" element={<UserCreate />} />
        <Route path="users/:id" element={<UserDetail />} />
        <Route path="feature-flags" element={<FeatureFlags />} />
        <Route path="dead-letters" element={<DeadLetters />} />
        {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && <Route path="dev/sms" element={<DevSmsPage />} />}
        <Route path="*" element={<Navigate to="/admin" replace />} />
      </Route>
    </Routes>
  );
}
```

Key changes:
- Drop `import AdminLayout from '@/components/layout/AdminLayout';`
- Add `Outlet` to the `react-router` import
- Add `import { AppShell } from '@/components/layout/AppShell';`
- Add `import { adminNav, adminBrand } from './navigation';`
- Add a local `AdminShell` wrapper that mounts `<AppShell variant="full" navConfig={adminNav} brand={adminBrand}>` around `<Outlet />`
- Change `<Route element={<AdminLayout />}>` to `<Route element={<AdminShell />}>`
- All child routes unchanged

- [ ] **Step 3: Delete `AdminLayout.tsx`**

Run: `git rm src/web/src/components/layout/AdminLayout.tsx`

- [ ] **Step 4: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean. The admin pages still render their in-body headers (untouched in this task), but they now render inside the full-variant `AppShell` instead of through the shim. Existing tests should still pass — they mount individual pages directly with `@/test/test-utils`'s `render` helper, not through the feature router.

- [ ] **Step 5: Smoke the app**

Run: `make dev` in the background. Open `/admin/organizations` in a browser and verify the sidebar + topbar chrome renders, page body still shows the old in-body header (that's expected — Task 6 replaces it). Kill `make dev` when done.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/admin/index.tsx src/web/src/components/layout/AdminLayout.tsx
git commit -m "refactor(web): mount AppShell directly from admin feature, delete AdminLayout shim"
```

---

## Phase 2 — List pages (Tasks 6–8)

Each List page drops its in-body header row (relocated to `<PageTopbar>`), swaps its three summary cards for three `<StatTile>` instances, applies the table restyle rules, and drops its outer `p-6 space-y-6` wrapper (AppShell content padding takes over). Per-page loading/error/empty UX is preserved verbatim — `OrgList` uses skeletons, `CourseList` uses text, and that distinction stays.

### Table restyle rules (apply identically to all three List pages)

| Target | Change |
|---|---|
| Outer wrapper `<div>` | `border rounded-md` → `border border-border-strong rounded-md bg-white overflow-hidden` |
| `<TableHeader>` row | Add `bg-canvas` |
| Each `<TableHead>` | Add `text-[10px] uppercase tracking-wider text-ink-muted` |
| First-column cell (entity name) | Replace `font-semibold` with `font-medium` where present |
| Numeric count cells | Add `font-mono text-[13px] text-ink` |
| Date cells | Replace `text-sm` with `font-mono text-[12px] text-ink-muted` |
| Clickable `<TableRow>` | Keep `cursor-pointer` — hover background resolves to `--muted` via cascade, no class change |
| Status `<Badge>` | Do NOT add explicit classes; switch any `bg-green-100 text-green-800` variants to the stock `variant="default"` so the cascade themes them |

---

### Task 6: Restyle `OrgList.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/OrgList.tsx`

`OrgList` is the reference implementation for the List pattern — it already uses skeleton loading. Drop the in-body header, replace the three summary cards with `<StatTile>`, restyle the table per the rules above, and drop the outer `p-6 space-y-6` wrapper.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/OrgList.tsx` in full (160 lines).

- [ ] **Step 2: Replace the entire file with the restyled version**

```tsx
import { Link, useNavigate } from 'react-router';
import { useOrganizations } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { StatTile } from '../components/StatTile';
import type { Organization } from '@/types/organization';

function TableRowSkeleton() {
  return (
    <TableRow>
      <TableCell><Skeleton className="h-4 w-40" /></TableCell>
      <TableCell><Skeleton className="h-4 w-12" /></TableCell>
      <TableCell><Skeleton className="h-4 w-12" /></TableCell>
      <TableCell className="hidden md:table-cell"><Skeleton className="h-4 w-24" /></TableCell>
    </TableRow>
  );
}

export default function OrgList() {
  const { data: orgs, isLoading, error, refetch } = useOrganizations();
  const navigate = useNavigate();

  const totalCourses = orgs?.reduce((sum, org) => sum + org.courseCount, 0) ?? 0;
  const totalUsers = orgs?.reduce((sum, org) => sum + org.userCount, 0) ?? 0;

  const loadingSkeleton = <Skeleton className="h-7 w-12 inline-block" />;

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Organizations</h1>}
        right={
          <Button asChild>
            <Link to="/admin/organizations/new">Create Organization</Link>
          </Button>
        }
      />

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <StatTile label="Total Organizations" value={isLoading ? loadingSkeleton : (orgs?.length ?? 0)} />
        <StatTile label="Total Courses" value={isLoading ? loadingSkeleton : totalCourses} />
        <StatTile label="Total Users" value={isLoading ? loadingSkeleton : totalUsers} />
      </div>

      {error && (
        <div className="flex items-center gap-4 mb-6">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Failed to load organizations'}
          </p>
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            Retry
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="border border-border-strong rounded-md bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Courses</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Users</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {Array.from({ length: 5 }).map((_, i) => (
                <TableRowSkeleton key={i} />
              ))}
            </TableBody>
          </Table>
        </div>
      ) : !orgs || orgs.length === 0 ? (
        <p className="text-ink-muted text-sm py-12 text-center">No organizations yet. Create one to get started.</p>
      ) : (
        <div className="border border-border-strong rounded-md bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Courses</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Users</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orgs.map((org: Organization) => (
                <TableRow
                  key={org.id}
                  className="cursor-pointer"
                  onClick={() => navigate(`/admin/organizations/${org.id}`)}
                >
                  <TableCell className="font-medium">{org.name}</TableCell>
                  <TableCell className="font-mono text-[13px] text-ink">{org.courseCount}</TableCell>
                  <TableCell className="font-mono text-[13px] text-ink">{org.userCount}</TableCell>
                  <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                    {new Date(org.createdAt).toLocaleDateString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
```

Key removals:
- `Card`, `CardContent`, `CardHeader`, `CardTitle` imports
- The local `SummaryCardSkeleton` component (no longer used — `StatTile` handles its own skeleton via `value`)
- The in-body `<div className="flex items-center justify-between">` header row
- The outer `<div className="p-6 space-y-6">` wrapper

Key additions:
- `PageTopbar` import and the `<PageTopbar middle=... right=... />` element
- `StatTile` import and three `<StatTile>` instances

- [ ] **Step 3: Verify `PageTopbar` is exported from `@/components/layout/PageTopbar`**

Run Grep for pattern `export.*PageTopbar` in `src/web/src/components/layout/PageTopbar.tsx`.
Expected: a named export `PageTopbar`. If it's a default export, adjust the import to `import PageTopbar from '@/components/layout/PageTopbar';`.

- [ ] **Step 4: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 5: Run tests**

Run: `pnpm --dir src/web test`
Expected: existing tests pass. There is no `OrgList.test.tsx`, so no locator updates needed.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/admin/pages/OrgList.tsx
git commit -m "feat(web): restyle OrgList to Fieldstone with topbar + StatTiles"
```

---

### Task 7: Restyle `CourseList.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/CourseList.tsx`

`CourseList` is structurally different from `OrgList`: it has text-based loading / error states (not skeletons), no summary cards at all today, and dual-render mobile/desktop table cells (e.g. `md:hidden` tenant name under the course name). Existing tests depend on:
- `"Loading courses..."` text
- `"Error: Network error"` text
- `"No courses registered yet."` text
- `getByRole('link', { name: 'Register Course' })`
- Column headers in order: Name / Organization / Location / Contact / Registered
- Tenant name appearing twice (desktop `hidden md:table-cell` + mobile `md:hidden`)
- `—` em dash when tenant name is missing

**All of these must be preserved.** The spec's List pattern calls for summary tiles, but `CourseList` does not have them today. Per the "no new functionality" rule, **do NOT add summary tiles to `CourseList`** — the spec's Section 3 says "ports its existing columns and tile set 1-for-1", and the existing tile set is empty.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/CourseList.tsx` in full (113 lines).

- [ ] **Step 2: Replace the entire file with the restyled version**

```tsx
import { Link, useNavigate } from 'react-router';
import { useCourses } from '../hooks/useCourses';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { Course } from '@/types/course';

export default function CourseList() {
  const navigate = useNavigate();
  const { data: courses, isLoading, error } = useCourses();

  const topbar = (
    <PageTopbar
      middle={<h1 className="font-display text-[18px] text-ink">All Registered Courses</h1>}
      right={
        <Button asChild>
          <Link to="/admin/courses/new">Register Course</Link>
        </Button>
      }
    />
  );

  if (isLoading) {
    return (
      <>
        {topbar}
        <p className="text-ink-muted">Loading courses...</p>
      </>
    );
  }

  if (error) {
    return (
      <>
        {topbar}
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load courses'}
        </p>
      </>
    );
  }

  return (
    <>
      {topbar}

      {!courses || courses.length === 0 ? (
        <p className="text-ink-muted text-sm py-12 text-center">No courses registered yet.</p>
      ) : (
        <div className="border border-border-strong rounded-md bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Organization</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Location</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Contact</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Registered</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {courses.map((course: Course) => (
                <TableRow
                  key={course.id}
                  className="cursor-pointer"
                  onClick={() => void navigate(`/admin/courses/${course.id}`)}
                >
                  <TableCell>
                    <div className="font-medium">{course.name}</div>
                    <div className="md:hidden text-sm text-ink-muted">
                      {course.tenantName || '—'}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    {course.tenantName || '—'}
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div className="space-y-0.5">
                      {course.streetAddress && (
                        <div className="text-sm">{course.streetAddress}</div>
                      )}
                      {(course.city || course.state || course.zipCode) && (
                        <div className="text-sm">
                          {course.city}
                          {course.city && course.state ? ', ' : ''}
                          {course.state} {course.zipCode}
                        </div>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div className="space-y-0.5">
                      {course.contactEmail && (
                        <div className="text-sm">{course.contactEmail}</div>
                      )}
                      {course.contactPhone && (
                        <div className="text-sm">{course.contactPhone}</div>
                      )}
                    </div>
                  </TableCell>
                  <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                    {new Date(course.createdAt).toLocaleDateString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
```

Key changes:
- Drop the in-body `<div className="flex items-center justify-between">` header row and the outer `<div className="p-6 space-y-6">` wrapper.
- Loading / error branches return the topbar + a text paragraph (no outer `p-6` wrapper).
- Empty state: `text-muted-foreground` → `text-ink-muted text-sm py-12 text-center`.
- Table restyle rules applied.
- First-column name: `font-semibold` → `font-medium`.
- Registered date cell: `text-sm` → `font-mono text-[12px] text-ink-muted`.
- Mobile `md:hidden` tenant name row under the course name kept verbatim — tests depend on it.
- No `StatTile` added (none existed before — no new functionality rule).

- [ ] **Step 3: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 4: Run `CourseList.test.tsx` specifically**

Run: `pnpm --dir src/web test CourseList`
Expected: all eight tests in `CourseList.test.tsx` pass. The tests use text-based and role-based queries that the restyle preserves:
- `getByText('Loading courses...')` — preserved
- `getByText('Error: Network error')` — preserved
- `getByText('No courses registered yet.')` — preserved
- `getByRole('link', { name: 'Register Course' })` — still there (moved into the topbar; `PageTopbar` renders via the AppShell slot but its children are in the DOM tree)
- Column headers preserved in same order
- Dual-render tenant names preserved
- Em dash preserved

If any test fails, STOP and investigate. Do NOT modify test assertions — fix the code.

- [ ] **Step 5: Run full test suite**

Run: `pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/admin/pages/CourseList.tsx
git commit -m "feat(web): restyle CourseList to Fieldstone with topbar"
```

---

### Task 8: Restyle `UserList.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/UserList.tsx`

Same treatment as Task 6 / 7, adapted to `UserList`'s current structure. Check the file first for whether it has summary cards today and whether it uses skeletons or text for loading. Apply the restyle rules uniformly.

Also sweep the hardcoded status Badge: `src/web/src/features/admin/pages/UserList.tsx:90` currently has `<Badge className="bg-green-100 text-green-800 hover:bg-green-100">Active</Badge>`. Replace with `<Badge>Active</Badge>` (stock `default` variant, themed via cascade to Fieldstone green-faint/green). For `Inactive`, use `<Badge variant="secondary">Inactive</Badge>`.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/UserList.tsx` in full. Identify:
- Does it have summary cards? If yes, replace them 1-for-1 with `<StatTile>` using the same labels and values. If no, don't add any.
- Does loading use skeletons or text? Preserve whatever's there.
- Does it have mobile dual-render? Preserve.
- Where is the hardcoded `bg-green-100 text-green-800` Badge (line 90 currently)?

- [ ] **Step 2: Apply the restyle in place**

Apply these transformations (use Edit tool for each, or a single Write if the whole file is simple):

1. Add `PageTopbar` import. If summary tiles are going to be used, add `StatTile` import.
2. Drop the in-body `<div className="flex items-center justify-between">` header row. Relocate the title and primary action to `<PageTopbar middle={<h1 className="font-display text-[18px] text-ink">Users</h1>} right={<Button asChild><Link to="/admin/users/new">Create User</Link></Button>} />`. (Confirm the exact title + button text from the current file — keep them verbatim.)
3. If summary cards exist today, replace with `<StatTile>` instances, using the exact same labels and values, with `isLoading` passing `<Skeleton className="h-7 w-12 inline-block" />` as the value.
4. Apply the table restyle rules to the table markup.
5. Replace `<Badge className="bg-green-100 text-green-800 hover:bg-green-100">Active</Badge>` with `<Badge>Active</Badge>`. If an `Inactive` branch exists, use `variant="secondary"`.
6. Drop the outer `p-6 space-y-6` (or similar) wrapper.
7. Convert the return body to a fragment `<>...</>` so the topbar is a sibling of the content.

- [ ] **Step 3: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean. There is no `UserList.test.tsx` in the current test suite, so no locator updates needed.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/UserList.tsx
git commit -m "feat(web): restyle UserList to Fieldstone with topbar"
```

---

## Phase 3 — Create pages (Tasks 9–11)

Each Create page drops its in-body Back + `<h1>` row, restyles the `<CardTitle>`, widens from `max-w-lg` to `max-w-2xl`, drops the outer `p-6` wrapper, and uses the new `DetailTitle` helper in the topbar. Form behavior (schemas, validation, mutations, navigation-on-success, field definitions) is preserved verbatim.

### Create pattern (reference — apply identically across all three Create tasks)

The body of every Create page after restyle looks like:

```tsx
<>
  <PageTopbar middle={<DetailTitle backTo="/admin/{entity}" title="Create {Entity}" />} />

  <div className="max-w-2xl">
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          {entity} Details
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            {/* existing fields, unchanged */}

            {createMutation.isError && (
              <p className="text-sm text-destructive">{/* existing message */}</p>
            )}

            <div className="flex gap-3">
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Creating...' : 'Create {Entity}'}
              </Button>
              <Button type="button" variant="outline" asChild>
                <Link to="/admin/{entity}">Cancel</Link>
              </Button>
            </div>
          </form>
        </Form>
      </CardContent>
    </Card>
  </div>
</>
```

Changes vs. current:
- Drop the in-body `<div className="mb-6 flex items-center gap-4">` Back + `<h1>` row
- Drop the `font-[family-name:var(--font-heading)]` inline-style class on the `<h1>`
- Outer wrapper: `<div className="p-6 max-w-lg">` → `<div className="max-w-2xl">`
- `<CardTitle>` gets `className="text-[11px] uppercase tracking-wider text-ink-muted font-normal"` override
- Button row `gap-4` → `gap-3`
- Convert any hardcoded color classes (e.g. `text-green-600`) to Fieldstone tokens (e.g. `text-green`). None are known in the three Create pages, but grep per-file as a check.

---

### Task 9: Restyle `OrgCreate.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/OrgCreate.tsx`

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/OrgCreate.tsx` in full (128 lines).

- [ ] **Step 2: Apply the restyle**

Replace the current return body with the Create pattern above, adapted for Organization. Keep the three form fields (`name`, `operatorEmail`, `sendInvite`) and the Send Invite checkbox verbatim — no field changes.

Specifically, replace the current `return` block (starting at `return ( <div className="p-6 max-w-lg">`) with:

```tsx
  return (
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
                <FormField
                  control={form.control}
                  name="name"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Organization Name *</FormLabel>
                      <FormControl>
                        <Input {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="operatorEmail"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>First Operator Email *</FormLabel>
                      <FormControl>
                        <Input type="email" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <FormField
                  control={form.control}
                  name="sendInvite"
                  render={({ field }) => (
                    <FormItem className="flex items-center gap-3 space-y-0">
                      <FormControl>
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={field.onChange}
                        />
                      </FormControl>
                      <FormLabel className="font-normal">
                        Send Invite to First Operator
                      </FormLabel>
                    </FormItem>
                  )}
                />

                {createMutation.isError && (
                  <p className="text-sm text-destructive">
                    {createMutation.error instanceof Error
                      ? createMutation.error.message
                      : 'Failed to create organization'}
                  </p>
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
  );
```

Also update the imports at the top of the file:
- Add: `import { PageTopbar } from '@/components/layout/PageTopbar';`
- Add: `import { DetailTitle } from '../components/DetailTitle';`

- [ ] **Step 3: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean. No `OrgCreate.test.tsx` exists, so no locator updates.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/OrgCreate.tsx
git commit -m "feat(web): restyle OrgCreate to Fieldstone with topbar + DetailTitle"
```

---

### Task 10: Restyle `CourseCreate.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/CourseCreate.tsx`

There are existing tests in `CourseCreate.test.tsx` that must stay green. Critical expectations to preserve:
- `getByText('Loading organizations...')` — loading state for the organization dropdown
- `getByText(/Error loading organizations/)` + `getByText(/Network error/)`
- `getByText('No organizations available')` + `getByText(/No organizations found/)` and `getByRole('link', { name: 'Create an organization' })`
- `getByRole('combobox', { name: 'Assign to Organization *' })` — the org dropdown
- `getByLabelText('Course Name *')`
- `getByRole('button', { name: 'Register Course' })` — the submit button
- DOM order: organization dropdown comes before course name input

**Note** that `CourseCreate` uses `Register Course` as its submit button label, not `Create Course`. Preserve verbatim.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/CourseCreate.tsx` in full.

- [ ] **Step 2: Apply the restyle**

Apply the Create pattern:
- Drop the in-body Back + `<h1>` row; add `<PageTopbar middle={<DetailTitle backTo="/admin/courses" title="Create Course" />} />`.
- Outer wrapper: `p-6 max-w-lg` (or similar) → `max-w-2xl`.
- Restyle `<CardTitle>`.
- Button row `gap-4` → `gap-3`.
- Preserve ALL form fields in their current order (organization dropdown FIRST, course name SECOND — the test explicitly asserts DOM order).
- Preserve the `Register Course` submit button text verbatim.
- Preserve all loading / error / empty state text strings verbatim — the tests assert them.
- Convert any hardcoded color classes (grep the file for `text-(green|amber|blue|red|yellow)-\d`).

- [ ] **Step 3: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 4: Run `CourseCreate.test.tsx` specifically**

Run: `pnpm --dir src/web test CourseCreate`
Expected: all tests in `CourseCreate.test.tsx` pass. If any fail, STOP and investigate — fix the code, never the assertions.

- [ ] **Step 5: Run full test suite**

Run: `pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/admin/pages/CourseCreate.tsx
git commit -m "feat(web): restyle CourseCreate to Fieldstone with topbar + DetailTitle"
```

---

### Task 11: Restyle `UserCreate.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/UserCreate.tsx`

Same treatment as Tasks 9 / 10. No known tests for `UserCreate`, so behavior is preserved-by-default.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/UserCreate.tsx` in full.

- [ ] **Step 2: Apply the restyle**

Apply the Create pattern:
- Drop the in-body Back + `<h1>` row; add `<PageTopbar middle={<DetailTitle backTo="/admin/users" title="Create User" />} />`.
- Outer wrapper: `p-6 max-w-lg` → `max-w-2xl`.
- Restyle `<CardTitle>`.
- Button row `gap-4` → `gap-3`.
- Preserve all form fields (including the organization dropdown / picker if present) verbatim in current order.
- Preserve submit button text verbatim.
- Preserve all loading / error / empty state text strings.
- Convert any hardcoded color classes (grep as a check).

- [ ] **Step 3: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/UserCreate.tsx
git commit -m "feat(web): restyle UserCreate to Fieldstone with topbar + DetailTitle"
```

---

## Phase 4 — Simple Detail pages (Tasks 12–13)

`CourseDetail` and `UserDetail` are single-pane Detail pages (no tabs — there's only one section per entity). Body is a single Fieldstone form panel, same idiom as the Create pattern.

### Simple Detail pattern (reference)

```tsx
<>
  <PageTopbar middle={<DetailTitle backTo="/admin/{entity}" title={entity?.name} />} />

  {error && (
    <div className="max-w-2xl">
      <p className="text-destructive">
        {error instanceof Error ? error.message : 'Failed to load {entity}'}
      </p>
    </div>
  )}

  <div className="max-w-2xl">
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          Details
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Form {...form}>
          {/* existing form, unchanged */}
        </Form>
      </CardContent>
    </Card>
  </div>
</>
```

Key changes:
- Drop the in-body `<div className="flex items-center gap-4">` Back + `<h1>` row
- Drop the `font-[family-name:var(--font-heading)]` inline-style class on the `<h1>`
- Drop the outer `<div className="p-6 space-y-6 max-w-3xl">` wrapper; replace with `<div className="max-w-2xl">` around the Card only
- `<CardTitle>` gets the uppercase 11 px muted override
- `text-green-600` → `text-green` (Fieldstone token) — known hit in `CourseDetail.tsx:181` and `UserDetail.tsx` Badge
- Badge sweep: any hardcoded `bg-green-100 text-green-800` → stock `<Badge>` (or `variant="secondary"` for Inactive)
- Loading state inside `CardContent` uses the existing skeleton treatment

---

### Task 12: Restyle `CourseDetail.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/CourseDetail.tsx`

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/CourseDetail.tsx` in full.

- [ ] **Step 2: Apply the Simple Detail pattern**

- Drop the in-body Back + `<h1>` row; add `<PageTopbar middle={<DetailTitle backTo="/admin/courses" title={course?.name} />} />`.
- Wrap the existing form Card in `<div className="max-w-2xl">`.
- Apply `className="border-border-strong"` to the Card.
- Apply the uppercase 11 px muted override to the `<CardTitle>`.
- Replace `text-green-600` (line 181 currently) with `text-green`.
- Preserve the entire form verbatim: `useForm`, Zod schema, mutation, field list, Save button, success/error states.
- Drop the outer wrapper.

Imports to add: `PageTopbar`, `DetailTitle`.

- [ ] **Step 3: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/CourseDetail.tsx
git commit -m "feat(web): restyle CourseDetail to Fieldstone simple-pane pattern"
```

---

### Task 13: Restyle `UserDetail.tsx`

**Files:**
- Modify: `src/web/src/features/admin/pages/UserDetail.tsx`

Same as Task 12, plus the Badge sweep at line 118.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/UserDetail.tsx` in full.

- [ ] **Step 2: Apply the Simple Detail pattern**

- Drop the in-body Back + `<h1>` row; add `<PageTopbar middle={<DetailTitle backTo="/admin/users" title={user?.name ?? user?.email} />} />`. (Use whatever display-name convention the current page uses — read and preserve.)
- Wrap the existing form Card in `<div className="max-w-2xl">` and apply `border-border-strong` + `<CardTitle>` override.
- Replace `<Badge className="bg-green-100 text-green-800 hover:bg-green-100">Active</Badge>` with `<Badge>Active</Badge>`. If an Inactive branch exists, use `<Badge variant="secondary">Inactive</Badge>`.
- Preserve all form fields, mutation, success/error states verbatim.
- Drop the outer wrapper.

Imports to add: `PageTopbar`, `DetailTitle`.

- [ ] **Step 3: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/pages/UserDetail.tsx
git commit -m "feat(web): restyle UserDetail to Fieldstone simple-pane pattern"
```

---

## Phase 5 — Tabbed Detail page (Task 14)

### Task 14: Restyle `OrgDetail.tsx` with `<Tabs>`

**Files:**
- Modify: `src/web/src/features/admin/pages/OrgDetail.tsx`

The largest behavioral change in Cluster 2. Today `OrgDetail` is a single scroll with three stacked Cards (Details / Courses / Users). After this task, it's a shadcn `<Tabs>` component with `defaultValue="details"` and the Courses and Users panels behind a click. No URL state; refresh always lands on Details.

Action buttons (`Register Course`, `Create User`) stay tab-scoped in their respective `CardHeader` slots — NOT page-scoped in the topbar. Their `?organizationId=<id>` query strings are preserved verbatim.

- [ ] **Step 1: Read current file**

Read `src/web/src/features/admin/pages/OrgDetail.tsx` in full (220 lines).

- [ ] **Step 2: Verify shadcn `Tabs` primitive is available**

Run Glob for `src/web/src/components/ui/tabs.tsx`.
Expected: file exists with exports `Tabs`, `TabsList`, `TabsTrigger`, `TabsContent`.

If missing: STOP. Adding a new primitive is out of scope and would require `pnpm dlx shadcn@latest add tabs`. Flag to the user before proceeding.

- [ ] **Step 3: Replace the entire file with the tabbed version**

```tsx
import { useParams, Link } from 'react-router';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useOrganization, useUpdateOrganization } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { DetailTitle } from '../components/DetailTitle';

const schema = z.object({
  name: z.string().min(1, 'Organization name is required'),
});

type FormData = z.infer<typeof schema>;

export default function OrgDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: org, isLoading, error } = useOrganization(id ?? '');
  const updateMutation = useUpdateOrganization();

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { name: '' },
  });

  useEffect(() => {
    if (org) {
      form.reset({ name: org.name });
    }
  }, [org, form]);

  function onSubmit(data: FormData) {
    if (!id) return;
    updateMutation.mutate({ id, ...data });
  }

  return (
    <>
      <PageTopbar middle={<DetailTitle backTo="/admin/organizations" title={org?.name} />} />

      {error && (
        <div className="max-w-4xl mb-4">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Failed to load organization'}
          </p>
        </div>
      )}

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
            <CardContent className="space-y-4">
              {isLoading ? (
                <div className="space-y-3">
                  <Skeleton className="h-4 w-32" />
                  <Skeleton className="h-9 w-full" />
                  <Skeleton className="h-4 w-48" />
                </div>
              ) : (
                <Form {...form}>
                  <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                    <FormField
                      control={form.control}
                      name="name"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Organization Name *</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {org && (
                      <p className="text-sm text-ink-muted">
                        Created {new Date(org.createdAt).toLocaleDateString()}
                      </p>
                    )}

                    {updateMutation.isError && (
                      <p className="text-sm text-destructive">
                        {updateMutation.error instanceof Error
                          ? updateMutation.error.message
                          : 'Failed to save changes'}
                      </p>
                    )}

                    {updateMutation.isSuccess && (
                      <p className="text-sm text-green">Changes saved.</p>
                    )}

                    <Button type="submit" disabled={updateMutation.isPending}>
                      {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
                    </Button>
                  </form>
                </Form>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="courses">
          <Card className="border-border-strong">
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
                Courses
              </CardTitle>
              {id && (
                <Button variant="outline" size="sm" asChild>
                  <Link to={`/admin/courses/new?organizationId=${id}`}>Register Course</Link>
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="p-4 space-y-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                </div>
              ) : !org?.courses.length ? (
                <p className="text-sm text-ink-muted p-4">No courses in this organization.</p>
              ) : (
                <div className="border-t border-border-strong overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-canvas">
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {org.courses.map((course) => (
                        <TableRow key={course.id}>
                          <TableCell>
                            <Link
                              to={`/admin/courses/${course.id}`}
                              className="font-medium hover:underline"
                            >
                              {course.name}
                            </Link>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="users">
          <Card className="border-border-strong">
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
                Users
              </CardTitle>
              {id && (
                <Button variant="outline" size="sm" asChild>
                  <Link to={`/admin/users/new?organizationId=${id}`}>Create User</Link>
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="p-4 space-y-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                </div>
              ) : !org?.users.length ? (
                <p className="text-sm text-ink-muted p-4">No users in this organization.</p>
              ) : (
                <div className="border-t border-border-strong overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-canvas">
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Email</TableHead>
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Role</TableHead>
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {org.users.map((user) => (
                        <TableRow key={user.id}>
                          <TableCell className="font-medium">{[user.firstName, user.lastName].filter(Boolean).join(' ') || user.email}</TableCell>
                          <TableCell className="text-sm text-ink-muted">{user.email}</TableCell>
                          <TableCell className="text-sm">{user.role}</TableCell>
                          <TableCell>
                            <Badge variant={user.isActive ? 'default' : 'secondary'}>
                              {user.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </>
  );
}
```

Key changes from the current file:
- Drop the outer `<div className="p-6 space-y-6 max-w-3xl">` wrapper.
- Drop the in-body `<div className="flex items-center gap-4">` Back + `<h1>` row.
- Replace three stacked Cards with `<Tabs defaultValue="details" className="max-w-4xl">` + three `<TabsContent>` panels.
- Each `<TabsContent>` wraps its Card in `border-border-strong` and its `<CardTitle>` gets the uppercase 11 px muted override.
- `text-green-600` → `text-green` (line 117 currently).
- Embedded tables restyled per Section 3 rules (uppercase tracked headers, `bg-canvas` header row).
- Inline `text-muted-foreground` → `text-ink-muted` where present.
- All form behavior, mutations, navigation, courses / users data display — unchanged.

- [ ] **Step 4: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 5: Run tests**

Run: `pnpm --dir src/web test`
Expected: clean. No `OrgDetail.test.tsx` exists, so no locator updates needed.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/admin/pages/OrgDetail.tsx
git commit -m "feat(web): restyle OrgDetail to Fieldstone with tabbed panels"
```

---

## Phase 6 — Finalize (Tasks 15–16)

### Task 15: Repair any test locators that broke and sweep residual hardcoded colors

**Files:**
- Modify (only if needed): `src/web/src/features/admin/__tests__/*.test.tsx`

Most tests should still pass after Phases 2–5 because:
- `PageTopbar` renders its children in the DOM tree (the topbar content appears via AppShell slot, not a separate document portal root), so `getByRole` queries for buttons and headings still find them.
- Loading/error/empty state text strings were preserved verbatim.
- Table column headers were preserved verbatim.
- Dual-render mobile rows were preserved.

However, two things can still break:
1. If `CourseList.test.tsx` queries by `getByRole('link', { name: 'Register Course' })` and the link is rendered via `PageTopbar`, the link might not be in the mounted tree because the tests mount `<CourseList />` in isolation without an `<AppShell>`. Investigate and fix by ensuring `PageTopbar` renders its slots as children even when there is no `AppShellContext` provider (check `PageTopbar.tsx` behavior in the absence of a provider).
2. Any lingering hardcoded colors.

- [ ] **Step 1: Run full test suite**

Run: `pnpm --dir src/web test`
Expected: ideally clean. If failures, list them and address in Step 2.

- [ ] **Step 2: Investigate `PageTopbar` behavior outside `AppShell`**

Read `src/web/src/components/layout/PageTopbar.tsx` to understand how it renders when there is no surrounding `AppShellContext`. If it renders nothing (because it depends on a context to inject into a slot), then `CourseList.test.tsx` will fail its `getByRole('link', { name: 'Register Course' })` assertion.

Two possible mitigations depending on what `PageTopbar` does:
- **Option A (preferred):** if `PageTopbar` renders its children inline when there is no provider, no action needed.
- **Option B:** if `PageTopbar` silently drops its children when there is no provider, update the affected test files (`CourseList.test.tsx`, `CourseCreate.test.tsx`) to use `render` with an explicit `AppShell` wrapper, OR update the test-utils `render` helper to provide a minimal `AppShellContext`, OR check if Cluster 1's walkup tests handled this already and mirror the approach.

**Do NOT modify test assertions.** Only modify test scaffolding (setup, mount, wrappers) to accommodate the new component structure.

- [ ] **Step 3: Sweep for any remaining hardcoded color classes in admin pages**

Run Grep for pattern `text-(green|amber|blue|red|yellow|orange|purple|pink|indigo|cyan|emerald|rose|sky|violet|fuchsia|teal|lime)-\d` inside `src/web/src/features/admin/pages`.

Expected hits: zero (all known hits were addressed in Tasks 12, 13, 14 and the Badge sweep in Task 8).

For any remaining hit: convert to the equivalent Fieldstone token (`text-green`, `text-ink`, `text-ink-muted`, `text-destructive`) per the design system. If unclear, flag to the user.

Also sweep for `bg-(green|amber|blue|red|yellow)-\d{2,3}` to catch any lingering Badge background overrides.

- [ ] **Step 4: Run lint + test**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: clean.

- [ ] **Step 5: Commit**

Only if Steps 2 or 3 produced changes:

```bash
git add <affected files>
git commit -m "fix(web): repair admin test scaffolding and sweep hardcoded colors"
```

If nothing changed, skip the commit.

---

### Task 16: Manual smoke + PR preparation

**Files:** none (operational)

This is a human task. Subagents should STOP before this task and hand off.

- [ ] **Step 1: Start the dev server**

Run: `make dev`
Wait for the API to come up on :5221 and the web on :3000.

- [ ] **Step 2: Click through the nine admin pages**

For each page, verify the topbar title renders in display font, no in-body `<h1>` is present, and the primary action (if any) is in the topbar right slot:

- `/admin/organizations` — verify topbar title + Create button, three `<StatTile>` render with mono numbers, table renders with uppercase headers + mono dates, row click navigates to detail
- `/admin/organizations/new` — verify topbar title + back chevron, form Card with uppercase section header, Create button submits and redirects back to list
- `/admin/organizations/<id>` — verify topbar shows org name, three tabs render (Details / Courses / Users), default tab is Details, switching tabs changes content, Save works, `Register Course` and `Create User` buttons still navigate with `?organizationId=<id>` preserved
- `/admin/courses` — list renders cleanly, `Register Course` button in topbar works
- `/admin/courses/new` — create form renders, submits correctly
- `/admin/courses/<id>` — detail single-pane form renders, Save works
- `/admin/users` — list renders cleanly
- `/admin/users/new` — create form renders, submits correctly
- `/admin/users/<id>` — detail single-pane form renders, Save works

- [ ] **Step 3: Sanity-check one operator page and one golfer page**

- Navigate to an operator page (e.g. `/operator` or the tee sheet) and verify it still renders — the shadow-token addition in Task 1 is at stock values so nothing should have moved.
- Navigate to a walk-up page (e.g. `/join/<course>`) and verify it still renders cleanly.

- [ ] **Step 4: Stop `make dev`**

- [ ] **Step 5: Take before/after screenshots**

Required by the PR description:
- One List page (e.g. `OrgList`)
- One Detail page (e.g. `OrgDetail` showing the tabs)
- One Create page (e.g. `OrgCreate`)

Capture "before" by temporarily stashing the branch and running the pre-cluster version, then restore. Or reference the Cluster 1 PR (#386) screenshots for the general shell treatment and just capture the new admin pages as "after". Whichever is faster.

- [ ] **Step 6: Open the PR**

Title: `feat(web): Fieldstone redesign — admin CRUD (Cluster 2)`

Body (use heredoc):

```markdown
## Summary

Restyles all nine admin CRUD pages (Org / Course / User × List / Detail / Create) to the Fieldstone design language inside the full-variant `AppShell`. Deletes the `AdminLayout.tsx` shim; admin routes now mount `<AppShell>` directly from `features/admin/index.tsx` with `adminBrand` colocated alongside `adminNav`. Each page contributes its topbar via `<PageTopbar>` and the body opens straight into content with no in-body header row.

Two page-internal helpers (`StatTile`, `DetailTitle`) land under `features/admin/components/` — domain-scoped, not foundation primitives.

Visual/structural only. No new endpoints, no new fields, no new dialogs, no new actions, no new unit or e2e tests.

Closes #383

## Foundation extension

This PR introduces two new foundation shadow tokens in `src/web/src/index.css` inside the existing `@theme inline` block:

\`\`\`css
--shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.05);
--shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
\`\`\`

Values are **stock Tailwind v4 defaults** — visual surface area is zero in this PR. Future clusters can flip Fieldstone shadows in one place without touching every component.

## Design spec

[`docs/superpowers/specs/2026-04-08-admin-crud-cluster-2-design.md`](../blob/chore/fieldstone-cluster-2-admin-crud/docs/superpowers/specs/2026-04-08-admin-crud-cluster-2-design.md)

## Screenshots

| | Before | After |
|---|---|---|
| List (OrgList) | ... | ... |
| Detail (OrgDetail, tabs) | ... | ... |
| Create (OrgCreate) | ... | ... |

## Verification

- [x] `pnpm --dir src/web lint` clean
- [x] `pnpm --dir src/web test` clean (existing tests; no new tests added per cluster rule)
- [x] Manual smoke across all nine admin pages
- [x] Operator tee sheet renders cleanly (shadow token sanity check)
- [x] Walkup join page renders cleanly (shadow token sanity check)

## Test plan

- [ ] Merge to main
- [ ] Deploy to test env
- [ ] Click through admin pages in test env
```

Run: `gh pr create --title "feat(web): Fieldstone redesign — admin CRUD (Cluster 2)" --body "$(cat <<'EOF' ... EOF)"`

---

## Done criteria

- [ ] Task 1: `--shadow-sm` and `--shadow` tokens added to `index.css` at stock values
- [ ] Task 2: `navigation.ts` renamed to `navigation.tsx` with `adminBrand` export
- [ ] Task 3: `StatTile.tsx` created in `features/admin/components/`
- [ ] Task 4: `DetailTitle.tsx` created in `features/admin/components/`
- [ ] Task 5: `AdminLayout.tsx` deleted; `features/admin/index.tsx` mounts `<AppShell>` directly
- [ ] Task 6: `OrgList` restyled with topbar, StatTiles, restyled table
- [ ] Task 7: `CourseList` restyled, mobile dual-render preserved, tests green
- [ ] Task 8: `UserList` restyled, Badge sweep done
- [ ] Task 9: `OrgCreate` restyled with topbar + DetailTitle
- [ ] Task 10: `CourseCreate` restyled, tests green
- [ ] Task 11: `UserCreate` restyled
- [ ] Task 12: `CourseDetail` restyled (single-pane), `text-green-600` swept
- [ ] Task 13: `UserDetail` restyled (single-pane), Badge + color sweep done
- [ ] Task 14: `OrgDetail` restyled with shadcn `<Tabs>`
- [ ] Task 15: Tests green, hardcoded color sweep clean
- [ ] Task 16: Manual smoke green on nine admin pages + one operator + one golfer; PR opened with `Closes #383`, foundation shadow-token callout, and before/after screenshots

---

## Self-review notes

**Spec coverage check (against Cluster 2 design spec sections):**
- Section 1 (architecture + shell removal) → Tasks 2, 5
- Section 2 (topbar pattern) → Tasks 6–14 (each page contributes topbar)
- Section 3 (list pattern + StatTile + table restyle) → Tasks 3, 6, 7, 8
- Section 4 (detail pattern + DetailTitle + tabbed OrgDetail) → Tasks 4, 12, 13, 14
- Section 5 (create pattern) → Tasks 9, 10, 11
- Section 6 (shadow tokens) → Task 1
- Section 7 (files + tests + smoke + rollout) → Tasks 15, 16
- Section 8 (risks) → addressed inline in each task (tab adoption note in Task 14; AdminLayout grep in Task 5; hardcoded color sweep in Tasks 8, 12, 13, 14, 15; max-w change called out in Create pattern)

**Placeholder scan:** all tasks have concrete file paths, commands, and code. No "TBD", no "implement later", no "fill in details". Tasks 8, 11, 13 (`UserList`, `UserCreate`, `UserDetail`) give less verbatim code than the Org counterparts because their current contents must be read first — this is intentional, and each task explicitly calls out "Read current file" as Step 1 and lists specific transformations to apply.

**Type consistency:** `StatTile` signature `{ label: string; value: ReactNode }` and `DetailTitle` signature `{ backTo: string; title?: string }` are used consistently in Tasks 3, 4, and all their callers (Tasks 6–14).

**Risks flagged in the spec that are explicitly addressed:**
- Tab adoption risk → Task 14 Step 2 verifies `Tabs` primitive exists before touching code
- AdminLayout deletion ripples → Task 5 Step 1 greps for all importers
- Shadow tokenization scope creep → Task 1 is tiny and isolated; PR description callout is in Task 16
- `max-w-lg → max-w-2xl` → documented in Phase 3 reference pattern
- `navigation.ts → navigation.tsx` git mv → Task 2 uses explicit `git mv`
- Hardcoded color sweep → Task 15 Step 3 does a final grep
