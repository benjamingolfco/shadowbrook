# Operator Long-Tail Redesign — Cluster 4

**Date:** 2026-04-08
**Branch:** `chore/fieldstone-cluster-4-operator-longtail`
**Tracking:** #385 (sub-issue of #381 — Operator/Admin redesign rollout)
**Foundation:** PR #380, [`docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`](2026-04-06-operator-admin-redesign-foundation-design.md)
**Precedents:** Cluster 1 PR #386, Cluster 2 PR #387, Cluster 3 PR #388

## Summary

Cluster 4 of the Fieldstone redesign rollout. The last cluster. Restyle the three operator long-tail pages — `CoursePortfolio`, `OrgPicker`, `TeeTimeSettings` — to use the Fieldstone design language, and remove **both** remaining layout shims (`OperatorLayout.tsx` and `WaitlistShellLayout.tsx`) so that the operator feature mounts `<AppShell>` directly at every route.

The operator feature has five AppShell mount sites today (three in `OperatorLayout`, two in `WaitlistShellLayout`), distributed across the role / feature-flag / course-state branches inside `features/operator/index.tsx`. All five must keep behaving exactly as today — same brand identity, same switch-course behavior, same routing. The shim removal is structural cleanup, not a behavior change.

The pages' data, hooks, mutations, schemas, and routing are unchanged. Only layout and visual treatment change. No new endpoints, no new aggregations, no new fields, no new dialogs, no new actions, no new unit or e2e tests.

## Out of scope

- Any change to `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, or any foundation primitive.
- Any change to `AdminLayout` or admin routes (fully removed in Cluster 2).
- Any restyle of `TeeSheet.tsx`, `WalkUpWaitlist.tsx`, or any admin page — all already shipped.
- Right rail. None of the three pages render `<PageRightRail>`.
- Mobile / small-screen layout. Desktop-first; deferred until usage data shows it matters.
- New product features (no new fields, no new actions, no new dialogs, no new endpoints, no new hooks, no new mutations).
- New unit or e2e tests. Existing tests stay; locators and mount harnesses update only where the shim removal or the restyle forces them.
- Rationalization of the operator-shell-by-feature-flag matrix. `CoursePortfolio` continues to render under `variant="full"` when `full-operator-app` is on and `variant="minimal"` when it is off, identical to today.

## Section 1 — Architecture & shell removal

### Both shims are deleted

- `src/web/src/components/layout/OperatorLayout.tsx` — deleted.
- `src/web/src/components/layout/WaitlistShellLayout.tsx` — deleted.

The cross-feature transitional layer introduced by the foundation PR (#380) goes away entirely. After this PR, `src/web/src/components/layout/` contains only `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, and `PageHeader.tsx`. No role-based layout wrappers remain.

**Note on scope.** The #385 issue text claims that after Cluster 1, `WaitlistShellLayout`'s only remaining consumer is `CoursePortfolio`. That is incorrect — Cluster 1 restyled `WalkUpWaitlist`'s contents but did not touch its mounting, so `WaitlistShellLayout` still wraps both `CoursePortfolio` and `WalkUpWaitlist` in the `!fullOperatorApp` branches of `features/operator/index.tsx`. Deleting the file requires fixing both mount sites. The restyled `WalkUpWaitlist` content is not touched; only its shell mount changes.

### Five mount sites in `features/operator/index.tsx`

| # | Branch | Wraps | Variant today | Variant after |
|---|---|---|---|---|
| 1 | `AdminOrgGate` (admin, no org) | `<OrgPicker>` | `OperatorLayout` (full) | `<AppShell variant="full">` |
| 2 | `CourseGate` (no course, `fullOperatorApp`) | `<CoursePortfolio>` | `OperatorLayout` (full) | `<AppShell variant="full">` |
| 3 | `CourseGate` (no course, `!fullOperatorApp`) | `<CoursePortfolio>` | `WaitlistShellLayout` (minimal) | `<AppShell variant="minimal">` |
| 4 | `CourseGate` (course, `!fullOperatorApp`) | `<WalkUpWaitlist>` | `WaitlistShellLayout` (minimal) | `<AppShell variant="minimal">` |
| 5 | `CourseGate` (course, `fullOperatorApp`) | `tee-sheet` / `waitlist` / `settings` | `OperatorLayout` (full) | `<AppShell variant="full">` |

All five mounts compute identical `brand`, `onSwitchCourse`, and (for full variant) `navConfig` props. That computation is extracted to a single hook.

### `useOperatorShellProps` hook

New file: `src/web/src/features/operator/hooks/useOperatorShellProps.tsx` (uses JSX — `.tsx`, not `.ts`).

```tsx
import { useCallback } from 'react';
import type { ComponentProps } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/features/auth';
import { useCourseContext } from '../context/CourseContext';
import { operatorNav, OperatorBrand, WaitlistBrand } from '../navigation';
import type { AppShell } from '@/components/layout/AppShell';

type Variant = 'full' | 'minimal';
type ShellProps = Pick<
  ComponentProps<typeof AppShell>,
  'variant' | 'brand' | 'onSwitchCourse' | 'navConfig'
>;

export function useOperatorShellProps(variant: Variant): ShellProps {
  const { user } = useAuth();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  return {
    variant,
    brand: variant === 'full' ? <OperatorBrand /> : <WaitlistBrand />,
    navConfig: variant === 'full' ? operatorNav : undefined,
    onSwitchCourse: showSwitchCourse ? handleSwitchCourse : undefined,
  };
}
```

Notes:

- **`variant` parameter** — caller chooses the shell variant. The hook does not inspect `useFeature('full-operator-app')` itself because the branching logic already lives in `features/operator/index.tsx`, and this hook is meant to be a thin prop computer, not a routing decision maker.
- **`onSwitchCourse` matches today's behavior exactly** — lifted verbatim from the two deleted shims. Both shims computed the identical `showSwitchCourse` predicate and the identical navigate-and-clear callback.
- **`brand` branches inside the hook**, not at the call site, to keep each mount site terse.
- **Type-safe `ShellProps`** — `ComponentProps<typeof AppShell>` picks the exact prop types off the real component, so a future change to `AppShell`'s props surfaces as a type error here.

### `features/operator/index.tsx` — one local wrapper, five uses

React Router's `<Route element={...}>` prop takes a ReactElement, not a render function. Calling a hook inside a `<Route element={}>` requires either calling the hook inside a component and passing that component as the element, or inlining the hook at every branch. A single tiny feature-local wrapper component avoids duplication:

```tsx
function OperatorShell({ variant, children }: { variant: 'full' | 'minimal'; children: ReactNode }) {
  const shellProps = useOperatorShellProps(variant);
  return <AppShell {...shellProps}>{children}</AppShell>;
}
```

**Why this is not a shim under a new name.** `OperatorLayout.tsx` lived in the shared `components/layout/` directory, carried the brand, switch-course, navConfig, and OrgSwitcher logic directly, and was imported by `features/operator/index.tsx`. `WaitlistShellLayout.tsx` did the same for the minimal variant. Both shims existed because the foundation PR couldn't migrate every page in one go. `OperatorShell` is different:

1. It lives **inside the feature module** (`features/operator/index.tsx`), not in the cross-feature `components/layout/` directory.
2. It is **5 lines**. It holds no brand logic, no switch-course logic, no nav config. All of that moved to `useOperatorShellProps` / `navigation.tsx`.
3. It exists **only** because React Router's `element` prop needs a component to host the hook call. It is the minimum glue, not an abstraction layer.

Each of the five mount sites uses `<OperatorShell variant="full|minimal">` around its routes:

```tsx
// AdminOrgGate
<Routes>
  <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
    <Route path="*" element={<OrgPicker />} />
  </Route>
</Routes>

// CourseGate, no course, fullOperatorApp
<Routes>
  <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
    <Route path="*" element={<CoursePortfolio />} />
  </Route>
</Routes>

// CourseGate, no course, !fullOperatorApp
<Routes>
  <Route element={<OperatorShell variant="minimal"><Outlet /></OperatorShell>}>
    <Route path="*" element={<CoursePortfolio />} />
  </Route>
</Routes>

// CourseGate, course, !fullOperatorApp
<Routes>
  <Route element={<OperatorShell variant="minimal"><Outlet /></OperatorShell>}>
    <Route path="*" element={<WalkUpWaitlist />} />
  </Route>
</Routes>

// CourseGate, course, fullOperatorApp
<Routes>
  <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
    <Route path="tee-sheet" element={<TeeSheet />} />
    <Route path="waitlist" element={<WalkUpWaitlist />} />
    <Route path="settings" element={<TeeTimeSettings />} />
    <Route path="*" element={<Navigate to="tee-sheet" replace />} />
  </Route>
</Routes>
```

The imports at the top of `features/operator/index.tsx` drop `OperatorLayout` and `WaitlistShellLayout` and add `AppShell`, `useOperatorShellProps`, and (via implicit JSX) `OperatorBrand` / `WaitlistBrand` (transitively through the hook).

## Section 2 — Brand colocation in `navigation.tsx`

### Rename `features/operator/navigation.ts → navigation.tsx`

Via `git mv` so history follows. The file now allows JSX.

### Existing export preserved

```tsx
export const operatorNav: NavConfig = {
  sections: [
    {
      label: 'Operations',
      items: [
        { to: '/operator/tee-sheet', label: 'Tee Sheet' },
        { to: '/operator/waitlist', label: 'Waitlist' },
      ],
    },
    {
      label: 'Management',
      items: [{ to: '/operator/settings', label: 'Settings' }],
    },
  ],
};
```

### New exports

The brand components lifted from the deleted shim files:

```tsx
import { useCallback } from 'react';
import { useNavigate } from 'react-router';
import { ChevronsUpDown } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from './context/CourseContext';
import { useOrgContext } from './context/OrgContext';

function OrgSwitcher() {
  // lifted verbatim from OperatorLayout.tsx — role-aware org dropdown for admins
  // uses useAuth, useOrgContext, useCourseContext, useNavigate
}

export function OperatorBrand() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  return (
    <>
      {isAdmin ? (
        <OrgSwitcher />
      ) : (
        <h1
          className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
          title={user?.organization?.name ?? 'Teeforce'}
        >
          {user?.organization?.name ?? 'Teeforce'}
        </h1>
      )}
      <Badge variant={isAdmin ? 'default' : 'success'} className="text-[10px] px-1.5 py-0">
        {isAdmin ? 'Admin' : 'Operator'}
      </Badge>
    </>
  );
}

export function WaitlistBrand() {
  const { course } = useCourseContext();
  const { user } = useAuth();
  const displayName = course?.name ?? user?.organization?.name ?? 'Teeforce';

  return (
    <span className="text-lg font-semibold font-[family-name:var(--font-heading)] text-ink">
      {displayName}
    </span>
  );
}
```

**No visual changes.** The brand components are lifted verbatim from the deleted shim files — same class names, same role branching, same `OrgSwitcher` dropdown behavior, same fallback chain. The only change is their location.

**Why `navigation.tsx` and not `features/operator/components/`.** Cluster 2 set the precedent of colocating admin's `adminBrand` with its nav config. Operator's brand is interactive (admins get a dropdown) where admin's was a static node, but the principle is the same: the nav and the brand together describe one feature's shell identity. A file-top comment on `navigation.tsx` calls this out so reviewers don't trip on finding a component in a file named `navigation`.

**`features/operator/components/` is not used** for the brand components. The operator feature already distinguishes between `components/` and colocated brand via the navigation file, matching the Cluster 2 admin precedent.

## Section 3 — `CoursePortfolio` restyle

### Today

- Centered (`flex h-full items-center justify-center`) `max-w-3xl` column.
- Per-state duplicated `<h1>"Select a Course"` header with an inline `font-[family-name:var(--font-heading)]` class.
- Clickable `<Card>`s with name, location subtitle, an `Active` badge, and a decorative `Manage` outline button as `CardAction`.
- Loading renders three skeleton cards, also centered.
- Error renders the `<h1>` + `text-destructive` message + Retry.
- Empty renders the `<h1>` + "No courses available" + "Contact your administrator."
- Uses `hover:bg-muted/50`.

### After

```tsx
return (
  <>
    <PageTopbar
      middle={<h1 className="font-display text-[18px] text-ink">Select a Course</h1>}
    />

    <div className="max-w-3xl">
      {coursesQuery.isLoading && (
        <div className="space-y-3" aria-busy="true" aria-label="Loading courses">
          {[1, 2, 3].map((i) => (
            <Card key={i} className="border-border-strong">
              <CardHeader>
                <Skeleton className="h-5 w-48" />
                <Skeleton className="h-4 w-32" />
              </CardHeader>
            </Card>
          ))}
        </div>
      )}

      {coursesQuery.isError && (
        <div className="space-y-4">
          <p className="text-destructive text-sm">
            Error loading courses: {coursesQuery.error.message}
          </p>
          <Button variant="outline" onClick={() => void coursesQuery.refetch()}>
            Retry
          </Button>
        </div>
      )}

      {!coursesQuery.isLoading && !coursesQuery.isError && coursesQuery.data?.length === 0 && (
        <p className="text-ink-muted text-sm py-12 text-center">
          No courses available. Contact your administrator to add a course.
        </p>
      )}

      {coursesQuery.data && coursesQuery.data.length > 0 && (
        <div className="space-y-3">
          {coursesQuery.data.map((course) => (
            <Card
              key={course.id}
              role="button"
              tabIndex={0}
              aria-label={`Manage ${course.name}, ${formatLocation(course)}`}
              className="border-border-strong cursor-pointer hover:bg-canvas transition-colors"
              onClick={() => handleSelectCourse(course)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                  e.preventDefault();
                  handleSelectCourse(course);
                }
              }}
            >
              <CardHeader>
                <CardTitle>{course.name}</CardTitle>
                <CardDescription>{formatLocation(course)}</CardDescription>
                <Badge variant="success" className="w-fit">
                  Active
                </Badge>
              </CardHeader>
            </Card>
          ))}
        </div>
      )}
    </div>
  </>
);
```

### What changes

- Title moves to `<PageTopbar>` (18 px `font-display` per Cluster 2 topbar convention).
- **Drop vertical centering.** The body sits at the top of the AppShell content region in a left-aligned `max-w-3xl` column. Cluster 2 list pages already sit at the top; CoursePortfolio joins the pattern.
- **Drop the per-state `<h1>`** — the topbar carries the title in all four states (loading / error / empty / success).
- **Drop the `Manage` `<Button>` `CardAction`.** The entire card is already `role="button"` with an aria-label; the button was decorative chrome.
- **Drop the `text-base` override on `<CardTitle>`** — let Fieldstone defaults rule.
- **`bg-muted/50 → bg-canvas`** on hover so the treatment reads consistently with `OrgPicker`.
- **`border-border-strong`** on every Card.
- **Empty state copy consolidated** to one line (previously a two-line "No courses available" + "Contact your administrator").
- **`useEffect` auto-select when `data.length === 1`** — preserved verbatim, no change.
- **`hasAutoSelected` ref** — preserved verbatim.
- **Drop the outer `flex h-full items-center justify-center`** and the outer `p-8` padding — AppShell handles it.

### Mount harness impact

`CoursePortfolio` now contributes to the topbar via `<PageTopbar>`. The page relies on AppShell providing an `AppShellContext` portal target. Mount tests must wrap in a shell (real `<AppShell>` or the same test harness Cluster 1/2/3 tests use) — see Section 6.

## Section 4 — `OrgPicker` restyle

### Today

- Outer `<div className="p-6">`.
- `<h2>"Select an organization"` in body.
- `grid gap-3 sm:grid-cols-2 lg:grid-cols-3` of `<Card>` tiles.
- Each card: `<Card hover:bg-accent><CardContent className="p-4"><span className="font-medium">`.
- No loading / error states — reads `organizations` synchronously from `useAuth`.

### After

```tsx
return (
  <>
    <PageTopbar
      middle={<h1 className="font-display text-[18px] text-ink">Select an Organization</h1>}
    />

    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      {organizations.map((org) => (
        <Card
          key={org.id}
          className="border-border-strong cursor-pointer hover:bg-canvas transition-colors"
          onClick={() => selectOrg({ id: org.id, name: org.name })}
        >
          <CardContent className="p-4">
            <span className="font-medium">{org.name}</span>
          </CardContent>
        </Card>
      ))}
    </div>
  </>
);
```

### What changes

- Title moves to `<PageTopbar>` middle slot, capitalized to "Select an Organization" to match Cluster 2's sentence-case-but-capitalized pattern for list titles.
- Drop the outer `<div className="p-6">` — AppShell padding handles it.
- Drop the in-body `<h2>`.
- `hover:bg-accent → hover:bg-canvas` for consistency with `CoursePortfolio`.
- `border-border-strong` on each Card.
- Grid breakpoints preserved.
- No new loading / error / empty states — `organizations` is still read synchronously from `useAuth()`.

## Section 5 — `TeeTimeSettings` restyle

### Today

- Outer `<div className="p-6 max-w-2xl">`.
- `<PageHeader title="Tee Time Settings" />` in body.
- Bare `<Form>` with no card chrome, containing interval `<Select>`, `firstTeeTime` and `lastTeeTime` `<Input type="time" />`, plus a Save button.
- Inline loading / error / success messages in body.
- `text-success` (hardcoded, not a Fieldstone token) on the success message.
- Empty state when `!course` renders a centered full-height `<p className="text-muted-foreground">` message.

### After

```tsx
return (
  <>
    <PageTopbar
      middle={<h1 className="font-display text-[18px] text-ink">Tee Time Settings</h1>}
    />

    {!course ? (
      <p className="text-ink-muted text-sm py-12 text-center">
        Select a course from the sidebar to configure settings.
      </p>
    ) : (
      <div className="max-w-2xl">
        <Card className="border-border-strong">
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Tee Time Configuration
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                {settingsQuery.isLoading && (
                  <p className="text-ink-muted text-sm">Loading settings…</p>
                )}
                {settingsQuery.isError && (
                  <p className="text-destructive text-sm">
                    Error loading settings: {settingsQuery.error.message}
                  </p>
                )}

                {/* existing FormFields verbatim: interval Select, firstTeeTime, lastTeeTime */}

                {updateMutation.isError && (
                  <p className="text-destructive text-sm">
                    Error: {updateMutation.error.message}
                  </p>
                )}
                {updateMutation.isSuccess && (
                  <p className="text-green text-sm">
                    Tee time settings saved successfully!
                  </p>
                )}

                <Button type="submit" disabled={updateMutation.isPending}>
                  {updateMutation.isPending ? 'Saving…' : 'Save Settings'}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>
    )}
  </>
);
```

### What changes

- Title moves to `<PageTopbar>` — no `<PageHeader>` in body.
- Form wrapped in `<Card className="border-border-strong">` with an uppercase-tracked `<CardTitle>` ("Tee Time Configuration"). Matches the Cluster 2 Detail/Create single-pane form panel idiom.
- **`text-success → text-green`** (hardcoded color swept to Fieldstone token).
- **Loading message** moves inline inside the form region (above the first field), styled `text-ink-muted text-sm`. Today it sits above the whole `<Form>`; it's still inside the card after the change, just below `<CardContent>`'s top padding.
- **Empty state** keeps the existing message string verbatim but restyles to `text-ink-muted text-sm py-12 text-center`. The topbar stays visible, so the user still knows where they are. No Card chrome around the empty state message.
- Drop the outer `<div className="p-6 max-w-2xl">` — AppShell handles outer padding; `max-w-2xl` moves to the inner div wrapping the Card (only when not empty).
- **All existing form behavior preserved verbatim** — `useForm`, `zodResolver`, `registerDirtyForm` / `unregisterDirtyForm` effects, `settingsQuery.data` reset effect, mutation, success / error flows. Field set unchanged.

## Section 6 — Tests

Per the cluster rule, no new `.test.tsx` or `.spec.ts` files are created. Existing tests are touched **only** when their locators or mount harnesses break.

### `features/operator/__tests__/OperatorLayout.test.tsx`

The file targets a component that no longer exists. **Rename to `OperatorBrand.test.tsx`** and re-target the tests at the `<OperatorBrand />` component in `features/operator/navigation.tsx`. The interesting assertions — admin vs operator role branching, `OrgSwitcher` dropdown opening, course/org name fallback, badge label — still describe real behavior. They're lifted verbatim with updated imports.

Assertions that targeted layout regions (sidebar rendering, nav items rendering, topbar chrome) belong to `AppShell`'s tests, not `OperatorBrand`'s. Those specific cases are deleted from the renamed file with a justification in the PR description per the test-integrity rule.

### `features/operator/__tests__/CoursePortfolio.test.tsx`

The page now uses `<PageTopbar>`, which portals into an `AppShellContext` target that must exist in the render tree. Update the test mount to wrap the page in `<AppShell variant="minimal">` (or whichever shell variant the existing Cluster 1/2/3 tests use — audit their mount harness during implementation and match the pattern).

Behavior assertions — cards render with course names, clicking a card calls `selectCourse` with the right args, auto-select on single course, empty/error/loading states — stay unchanged. Locator updates only where the `<h1>` text is now in a topbar portal (still findable by `role="heading"` because portals render into the same DOM tree).

### `features/operator/__tests__/OperatorFeature.test.tsx`

Feature-level routing test. If it imports `OperatorLayout` or `WaitlistShellLayout` directly, swap those imports for the corresponding AppShell mount expectations. If it asserts on specific wrapper elements, re-target to assert on the inner pages' rendered content. Keep behavior assertions intact.

### Other tests

Grep `src/web/src` for imports of `OperatorLayout` and `WaitlistShellLayout`. Any file that imports either breaks at build time and needs updating. The only known consumers today are `features/operator/index.tsx`, `OperatorLayout.test.tsx`, and `OperatorFeature.test.tsx`.

### E2E tests

Audit `e2e/` for operator-shell specs. Likely-affected anchors:

- `TeeTimeSettings` `<h1>` — still findable by `role="heading"`, now rendered into the topbar portal.
- `CoursePortfolio` `<h1>` — same.
- `OrgPicker` `<h2>"Select an organization"` → `<h1>"Select an Organization"`. Text is case-sensitive for some matchers; update if any spec locates by exact case.
- Any Playwright selector that walks the DOM from a specific layout wrapper class (e.g. a hand-written CSS selector rooted at `OperatorLayout`) — rewrite to role-based locators per the project convention.
- The `Manage` button on `CoursePortfolio` is gone — any e2e test that clicks `getByRole('button', { name: 'Manage' })` must click the card directly instead (the card has `role="button"` and an accessible name, so `getByRole('button', { name: /Manage .* at .* / })` or equivalent aria-label match still works).

Run operator e2e specs locally before opening the PR.

## Section 7 — Files summary

### Files created

```
src/web/src/features/operator/hooks/useOperatorShellProps.tsx
```

### Files modified

```
src/web/src/features/operator/navigation.ts → navigation.tsx    (git mv)
  - Existing operatorNav export unchanged
  - New: OperatorBrand component (lifted from OperatorLayout.tsx, including OrgSwitcher)
  - New: WaitlistBrand component (lifted from WaitlistShellLayout.tsx)
  - File-top comment explaining why brand components colocate with nav config

src/web/src/features/operator/index.tsx
  - Drop imports of OperatorLayout, WaitlistShellLayout
  - Add imports of AppShell, useOperatorShellProps
  - Add local OperatorShell wrapper component (5 lines)
  - Replace all 5 <Route element={<X />}> uses with <Route element={<OperatorShell variant="...">...</OperatorShell>}>

src/web/src/features/operator/pages/CoursePortfolio.tsx
  - Drop outer centered wrapper; sit at top of content region
  - Move title to <PageTopbar> middle slot
  - Drop per-state duplicated <h1>
  - Drop Manage button CardAction
  - Apply border-border-strong + hover:bg-canvas on every Card
  - Consolidate empty-state copy
  - Drop text-base override on CardTitle

src/web/src/features/operator/pages/OrgPicker.tsx
  - Drop outer p-6 wrapper
  - Move title to <PageTopbar> middle slot (capitalized "Select an Organization")
  - Apply border-border-strong + hover:bg-canvas on every Card

src/web/src/features/operator/pages/TeeTimeSettings.tsx
  - Drop outer p-6 wrapper; move max-w-2xl to inner Card wrapper
  - Move title to <PageTopbar> middle slot
  - Wrap form in <Card border-border-strong> with uppercase-tracked <CardTitle>
  - Move loading / error messages inside the form block
  - text-success → text-green
  - Restyle !course empty state as centered muted message (topbar still visible)

src/web/src/features/operator/__tests__/OperatorLayout.test.tsx → OperatorBrand.test.tsx
  - Rename via git mv
  - Re-target at <OperatorBrand />; preserve behavior assertions
  - Drop layout-region assertions (belong in AppShell tests) with PR justification

src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx
  - Update mount harness to provide AppShellContext (wrap in <AppShell> or use same harness as Cluster 1/2/3 tests)
  - Preserve behavior assertions verbatim

src/web/src/features/operator/__tests__/OperatorFeature.test.tsx
  - Drop imports of deleted layouts
  - Update any wrapper-element assertions to assert on rendered page content
  - Preserve behavior assertions
```

### Files deleted

```
src/web/src/components/layout/OperatorLayout.tsx
src/web/src/components/layout/WaitlistShellLayout.tsx
```

### Files NOT touched

- `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, `PageHeader.tsx` — foundation primitives.
- All shadcn primitives in `src/web/src/components/ui/` — read-only per convention.
- `StatusBadge.tsx`, `StatusChip.tsx`, `PanelSection.tsx` — foundation wrappers.
- `TeeSheet.tsx`, `WalkUpWaitlist.tsx`, all admin pages — already shipped in foundation / Clusters 1–3.
- `useCourses`, `useTeeTimeSettings`, `useAuth`, `useOrgContext`, `useCourseContext`, `useFeature` and the API client — no data layer changes.
- Backend code, types, query keys — untouched.

## Section 8 — Risks

1. **`OrgSwitcher` is an interactive stateful component inside `navigation.tsx`.** Having an interactive component in a file named `navigation` may surprise reviewers. **Mitigation:** file-top comment explains the colocation decision and cites the Cluster 2 precedent. If the surprise value is high in review, an alternative is extracting `OrgSwitcher` into its own file under `features/operator/components/` and importing it into `navigation.tsx` — behavior is identical, only the file layout changes.

2. **Five mount sites depending on one hook** means a future requirement to diverge prop computation between variants (e.g. a variant that suppresses switch-course) ripples through the hook. **Mitigation:** accepted — today all five use the same output; divergence grows the hook's parameters when it arrives.

3. **`AppShellContext` requirement in tests.** Any existing test mount that renders a page standalone (no shell) will silently fail to receive portal content. Tests that assert on topbar-rendered titles will break. **Mitigation:** document the mount harness pattern in the PR description; update affected tests in one pass; audit Cluster 1/2/3 test files for the harness they use so this cluster matches.

4. **Feature flag (`full-operator-app`) × shell variant matrix** means `CoursePortfolio` renders in either shell depending on the flag. Both variants must pass visual QA. **Mitigation:** smoke both flag states during `make dev` manual QA.

5. **`OperatorLayout.tsx` / `WaitlistShellLayout.tsx` deletion ripples.** Any stray test, story, or feature file importing either breaks at build time. **Mitigation:** grep for both names before deleting; remove all references in one commit; `pnpm --dir src/web lint` and `pnpm --dir src/web test` catch the rest.

6. **The #385 issue text is wrong about Cluster 1 leaving `CoursePortfolio` as `WaitlistShellLayout`'s only consumer.** `WalkUpWaitlist` is still wrapped in that shim after Cluster 1. This spec corrects the scope — deleting `WaitlistShellLayout.tsx` requires fixing two mount sites, not one. The PR description calls this out explicitly so the mismatch between issue text and actual scope doesn't confuse reviewers.

7. **`WaitlistBrand` renders inside the operator feature.** `WaitlistBrand` was defined inside `components/layout/WaitlistShellLayout.tsx` today. Moving it into `features/operator/navigation.tsx` is the natural home because the minimal shell is only mounted inside the operator feature — no other feature uses it. If that stops being true later, `WaitlistBrand` moves again.

## Done criteria

- [ ] `src/web/src/components/layout/OperatorLayout.tsx` deleted
- [ ] `src/web/src/components/layout/WaitlistShellLayout.tsx` deleted
- [ ] `src/web/src/components/layout/` contains only `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, `PageHeader.tsx`
- [ ] `features/operator/navigation.ts` renamed to `navigation.tsx` via `git mv`
- [ ] `navigation.tsx` exports `operatorNav`, `OperatorBrand`, and `WaitlistBrand`
- [ ] `OrgSwitcher` logic lifted verbatim into `navigation.tsx`
- [ ] `features/operator/hooks/useOperatorShellProps.tsx` created; returns `{ variant, brand, onSwitchCourse, navConfig? }` typed via `ComponentProps<typeof AppShell>`
- [ ] `features/operator/index.tsx` has one local `<OperatorShell>` wrapper component (≤10 lines) calling the hook
- [ ] All 5 `<Routes>` blocks in `features/operator/index.tsx` use `<OperatorShell variant="...">` instead of the deleted layouts
- [ ] `CoursePortfolio.tsx` restyled per Section 3
- [ ] `OrgPicker.tsx` restyled per Section 4
- [ ] `TeeTimeSettings.tsx` restyled per Section 5
- [ ] All hardcoded colors (`text-success`, `text-muted-foreground` on the empty states, etc.) on these three pages converted to Fieldstone tokens where appropriate
- [ ] `hover:bg-muted/50` and `hover:bg-accent` on Card rows replaced with `hover:bg-canvas`
- [ ] No source edits to any file in `src/web/src/components/ui/`
- [ ] No source edits to `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, or `PageHeader.tsx`
- [ ] `OperatorLayout.test.tsx` renamed to `OperatorBrand.test.tsx`; tests re-target `<OperatorBrand />`; behavior assertions preserved; layout-region assertions deleted with PR justification
- [ ] `CoursePortfolio.test.tsx` mount harness updated to provide `AppShellContext`; behavior assertions preserved
- [ ] `OperatorFeature.test.tsx` updated to drop imports of deleted layouts; behavior assertions preserved
- [ ] `pnpm --dir src/web lint` clean
- [ ] `pnpm --dir src/web test` clean (no new tests; locator/harness updates only where forced)
- [ ] Manual smoke (`make dev`) green for:
  - `/operator` as admin with multiple orgs — `OrgPicker` renders, selecting an org proceeds to course pick
  - `/operator` as operator with multiple courses, `fullOperatorApp = true` — `CoursePortfolio` renders in full shell, selecting proceeds to tee sheet
  - `/operator` as walkup-only operator, `fullOperatorApp = false` — `CoursePortfolio` renders in minimal shell, selecting proceeds to `WalkUpWaitlist`
  - `/operator/settings` with a course selected — form renders in Card
  - `/operator/settings` with no course selected — centered muted empty-state message renders
  - `/operator/tee-sheet` — unchanged (foundation page) still works
  - `/operator/waitlist` — unchanged (Cluster 1 page) still works
  - One admin page (e.g. `/admin/organizations`) — unchanged (Cluster 2 page) still works
  - One golfer page — unchanged, no visual regression
- [ ] PR opened with `Closes #385`, before/after screenshots of all three pages, link to this spec, explicit callout that both `OperatorLayout` and `WaitlistShellLayout` shims are removed, and a note that the issue text was wrong about `WaitlistShellLayout`'s remaining consumers
