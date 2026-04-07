# Operator Long-Tail Redesign — Cluster 4 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle `CoursePortfolio`, `OrgPicker`, and `TeeTimeSettings` to the Fieldstone design language and delete both `OperatorLayout.tsx` and `WaitlistShellLayout.tsx` shims, with the operator feature mounting `<AppShell>` directly at all five route branches via a single `useOperatorShellProps` hook.

**Architecture:** A new `useOperatorShellProps(variant)` hook in `features/operator/hooks/` returns the `{ variant, brand, navConfig, onSwitchCourse }` shape for `<AppShell>`. A 5-line `OperatorShell` wrapper component, defined locally inside `features/operator/index.tsx`, calls the hook from inside the React Router `<Route element={...}>` tree. Brand components (`OperatorBrand`, `WaitlistBrand`, plus the interactive `OrgSwitcher`) move from the deleted shim files into `features/operator/navigation.tsx` (renamed from `.ts` via `git mv`), where they colocate with `operatorNav` per the Cluster 2 admin precedent. The three target pages drop in-body header rows, contribute their title via `<PageTopbar>`, drop outer `p-6` wrappers (AppShell handles padding), and apply Cluster 2/3 visual conventions (`border-border-strong`, `hover:bg-canvas`, `font-display` titles).

**Tech Stack:** React 19 + TypeScript 5.9, React Router 7 (`react-router`, not `react-router-dom`), TanStack Query, React Hook Form + Zod, Tailwind CSS, shadcn/ui (vendored, read-only). Vitest + React Testing Library. The test harness in `src/web/src/test/test-utils.tsx` already provides a `TestAppShellProvider` with portal target divs, so pages using `<PageTopbar>` work in tests without additional wrapping.

**Spec:** [`docs/superpowers/specs/2026-04-08-operator-longtail-cluster-4-design.md`](../specs/2026-04-08-operator-longtail-cluster-4-design.md)
**Tracking:** #385 (sub-issue of #381)
**Branch:** `chore/fieldstone-cluster-4-operator-longtail` (already created in worktree `.worktrees/cluster-4/`)
**Working directory:** `/home/aaron/dev/orgs/benjamingolfco/teeforce/.worktrees/cluster-4`

---

## File Structure

### Created
- `src/web/src/features/operator/hooks/useOperatorShellProps.tsx` — hook returning AppShell prop shape; one responsibility (compute shell props for the operator feature).

### Modified
- `src/web/src/features/operator/navigation.ts` → `navigation.tsx` (`git mv`) — adds `OperatorBrand`, `WaitlistBrand`, and the inner `OrgSwitcher` component, lifted verbatim from the deleted shim files.
- `src/web/src/features/operator/index.tsx` — drops layout imports, adds `OperatorShell` local wrapper, replaces all 5 mount sites.
- `src/web/src/features/operator/pages/CoursePortfolio.tsx` — restyle.
- `src/web/src/features/operator/pages/OrgPicker.tsx` — restyle.
- `src/web/src/features/operator/pages/TeeTimeSettings.tsx` — restyle.
- `src/web/src/features/operator/__tests__/OperatorLayout.test.tsx` → `OperatorBrand.test.tsx` (`git mv`) — re-target at `<OperatorBrand />`.
- `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx` — update two assertions (consolidated empty-state copy + removed Manage button).
- `src/web/src/features/operator/__tests__/OperatorFeature.test.tsx` — drop deleted layout imports/mocks.

### Deleted
- `src/web/src/components/layout/OperatorLayout.tsx`
- `src/web/src/components/layout/WaitlistShellLayout.tsx`

### Untouched
- `AppShell.tsx`, `AppShellContext.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, `PageHeader.tsx` (foundation primitives)
- All shadcn primitives in `src/web/src/components/ui/`
- All admin pages, all shipped operator pages (`TeeSheet`, `WalkUpWaitlist`)
- `useCourses`, `useTeeTimeSettings`, `useAuth`, all contexts, all queries

---

## Conventions you must follow

1. **`pnpm` only**, never `npm` or `yarn`. From the worktree root: `pnpm --dir src/web <command>`.
2. **Imports** use the `@/*` path alias (`@/components/ui/button`, never relative paths into `src/`).
3. **React Router import** is `react-router`, never `react-router-dom`.
4. **Tests use `render` from `@/test/test-utils`** (it provides `QueryClient`, `MemoryRouter`, and `TestAppShellProvider`).
5. **shadcn primitives in `src/web/src/components/ui/` are read-only.** Theme via tokens / class overrides at the call site.
6. **Frequent commits.** Each task ends with a commit. Don't bundle.
7. **Zod v4** — `import { z } from 'zod/v4'` is the convention, but `TeeTimeSettings.tsx` currently uses `'zod'` and you should not change that import in this cluster (out of scope; visual restyle only).
8. **Test integrity** — assertion changes require justification in the PR description. Two CoursePortfolio assertions change deliberately in this cluster (Task 8 documents both).
9. **No new tests, no new endpoints, no new aggregations, no new fields.** Visual / structural only.

---

## Task 1: Create `navigation.tsx` with brand components

**Files:**
- Rename: `src/web/src/features/operator/navigation.ts` → `src/web/src/features/operator/navigation.tsx`
- Modify (after rename): `src/web/src/features/operator/navigation.tsx`

**Why this task is first:** every later task depends on `OperatorBrand` and `WaitlistBrand` existing in `navigation.tsx`. Lift them now, verbatim from the shim files, so behavior is unchanged.

- [ ] **Step 1: Rename via `git mv`**

```bash
git mv src/web/src/features/operator/navigation.ts src/web/src/features/operator/navigation.tsx
```

- [ ] **Step 2: Verify rename**

Run: `git status`
Expected: `renamed: src/web/src/features/operator/navigation.ts -> src/web/src/features/operator/navigation.tsx`

- [ ] **Step 3: Replace file contents**

Overwrite `src/web/src/features/operator/navigation.tsx` with:

```tsx
// navigation.tsx — operator feature shell config.
//
// This file colocates `operatorNav` (the AppShell sidebar nav config) with the
// brand components (`OperatorBrand`, `WaitlistBrand`) that render in the AppShell
// header for the operator feature. The brand components are interactive (admins
// get an `OrgSwitcher` dropdown), so they are real React components rather than
// static ReactNodes — that's why this file is `.tsx`.
//
// The colocation pattern matches `features/admin/navigation.tsx` (Cluster 2),
// where `adminBrand` lives next to `adminNav`. The principle: a feature's
// nav and brand together describe one shell identity, and they belong in one place.

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
import { useOrgContext } from './context/OrgContext';
import { useCourseContext } from './context/CourseContext';
import type { NavConfig } from '@/components/layout/AppShell';

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

function OrgSwitcher() {
  const { organizations } = useAuth();
  const { org, selectOrg, clearOrg } = useOrgContext();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const handleSelect = useCallback(
    (selected: { id: string; name: string }) => {
      clearCourse();
      selectOrg({ id: selected.id, name: selected.name });
      navigate('/operator');
    },
    [clearCourse, selectOrg, navigate],
  );

  const handleClear = useCallback(() => {
    clearCourse();
    clearOrg();
    navigate('/operator');
  }, [clearCourse, clearOrg, navigate]);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1 text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground hover:bg-sidebar-accent rounded-md px-1 -mx-1"
        >
          <span className="max-w-[180px] truncate" title={org?.name ?? 'Select org'}>
            {org?.name ?? 'Select org'}
          </span>
          <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        {organizations.map((o) => (
          <DropdownMenuItem
            key={o.id}
            onSelect={() => handleSelect(o)}
            className={o.id === org?.id ? 'bg-accent' : ''}
          >
            {o.name}
          </DropdownMenuItem>
        ))}
        {org && (
          <DropdownMenuItem onSelect={handleClear} className="text-muted-foreground">
            Back to org list
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
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

- [ ] **Step 4: Verify lint**

Run: `pnpm --dir src/web lint`
Expected: 0 errors. (There may be pre-existing warnings unrelated to this change.)

- [ ] **Step 5: Verify type-check by running tests**

Run: `pnpm --dir src/web test --run features/operator`
Expected: existing operator tests still pass (no new tests yet; this just verifies the rename + new exports compile). `OperatorLayout.test.tsx` will still pass because it imports `@/components/layout/OperatorLayout` which still exists at this point.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/operator/navigation.tsx
git commit -m "feat(web): colocate OperatorBrand/WaitlistBrand in navigation.tsx (cluster 4)"
```

---

## Task 2: Create `useOperatorShellProps` hook

**Files:**
- Create: `src/web/src/features/operator/hooks/useOperatorShellProps.tsx`

The hook computes `{ variant, brand, navConfig, onSwitchCourse }` for `<AppShell>`. It is a `.tsx` file because it returns JSX (the brand component instances).

- [ ] **Step 1: Create the hook**

Write `src/web/src/features/operator/hooks/useOperatorShellProps.tsx`:

```tsx
import { useCallback } from 'react';
import type { ComponentProps } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/features/auth';
import { useCourseContext } from '../context/CourseContext';
import { OperatorBrand, WaitlistBrand, operatorNav } from '../navigation';
import { AppShell } from '@/components/layout/AppShell';

type Variant = 'full' | 'minimal';

type ShellProps = Pick<
  ComponentProps<typeof AppShell>,
  'variant' | 'brand' | 'onSwitchCourse' | 'navConfig'
>;

/**
 * Returns the prop shape that the operator feature passes to <AppShell>.
 *
 * Used by all five operator route branches in `features/operator/index.tsx`.
 * The hook centralizes the brand selection (full vs minimal variant get
 * different brand components) and the switch-course callback, both of which
 * used to live in the deleted `OperatorLayout.tsx` and `WaitlistShellLayout.tsx`
 * shims.
 */
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

- [ ] **Step 2: Verify type-check + lint**

Run: `pnpm --dir src/web lint`
Expected: 0 errors.

Run: `pnpm --dir src/web test --run features/operator`
Expected: existing tests still pass; new file compiles.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/hooks/useOperatorShellProps.tsx
git commit -m "feat(web): add useOperatorShellProps hook (cluster 4)"
```

---

## Task 3: Replace 5 mount sites in `features/operator/index.tsx`

**Files:**
- Modify: `src/web/src/features/operator/index.tsx`

Replace all five `<Route element={<OperatorLayout />}>` and `<Route element={<WaitlistShellLayout />}>` mounts with `<Route element={<OperatorShell variant="...">...</OperatorShell>}>`. The local `OperatorShell` wrapper component is the minimum glue needed for the React Router `element` prop to call `useOperatorShellProps()`.

- [ ] **Step 1: Overwrite the file**

Replace the entire contents of `src/web/src/features/operator/index.tsx` with:

```tsx
import { useEffect, type ReactNode } from 'react';
import { Routes, Route, Navigate, Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { useOperatorShellProps } from './hooks/useOperatorShellProps';
import TeeSheet from './pages/TeeSheet';
import TeeTimeSettings from './pages/TeeTimeSettings';
import WalkUpWaitlist from './pages/WalkUpWaitlist';
import CoursePortfolio from './pages/CoursePortfolio';
import OrgPicker from './pages/OrgPicker';
import { CourseProvider, useCourseContext } from './context/CourseContext';
import { OrgProvider, useOrgContext } from './context/OrgContext';
import { ThemeProvider } from '@/components/ThemeProvider';
import { useFeature } from '@/hooks/use-features';
import { useAuth } from '@/features/auth';

/**
 * Local wrapper component that lets us call the `useOperatorShellProps` hook
 * from inside React Router's `element` prop. Each operator route branch picks
 * a variant ('full' or 'minimal'); the hook supplies the brand, nav config,
 * and switch-course handler.
 *
 * This is the minimum glue needed to satisfy React Router's element-prop
 * contract — it carries no logic of its own and replaces the deleted
 * `OperatorLayout.tsx` and `WaitlistShellLayout.tsx` shims.
 */
function OperatorShell({ variant, children }: { variant: 'full' | 'minimal'; children: ReactNode }) {
  const shellProps = useOperatorShellProps(variant);
  return <AppShell {...shellProps}>{children}</AppShell>;
}

function OrgGate() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  if (isAdmin) {
    return <AdminOrgGate />;
  }

  return <CourseGate />;
}

function AdminOrgGate() {
  const { org } = useOrgContext();

  if (!org) {
    return (
      <Routes>
        <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
          <Route path="*" element={<OrgPicker />} />
        </Route>
      </Routes>
    );
  }

  return <CourseGate />;
}

function CourseGate() {
  const { course, clearCourse } = useCourseContext();
  const { courses } = useAuth();
  const fullOperatorApp = useFeature('full-operator-app', course?.id);

  useEffect(() => {
    if (course && !courses.some((c) => c.id === course.id)) {
      clearCourse();
    }
  }, [course, courses, clearCourse]);

  if (!course) {
    if (fullOperatorApp) {
      return (
        <Routes>
          <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
            <Route path="*" element={<CoursePortfolio />} />
          </Route>
        </Routes>
      );
    }

    return (
      <Routes>
        <Route element={<OperatorShell variant="minimal"><Outlet /></OperatorShell>}>
          <Route path="*" element={<CoursePortfolio />} />
        </Route>
      </Routes>
    );
  }

  if (!fullOperatorApp) {
    return (
      <Routes>
        <Route element={<OperatorShell variant="minimal"><Outlet /></OperatorShell>}>
          <Route path="*" element={<WalkUpWaitlist />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="waitlist" element={<WalkUpWaitlist />} />
        <Route path="settings" element={<TeeTimeSettings />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
    </Routes>
  );
}

export default function OperatorFeature() {
  return (
    <ThemeProvider>
      <OrgProvider>
        <CourseProvider>
          <OrgGate />
        </CourseProvider>
      </OrgProvider>
    </ThemeProvider>
  );
}
```

- [ ] **Step 2: Verify lint and that operator pages still type-check**

Run: `pnpm --dir src/web lint`
Expected: 0 errors.

- [ ] **Step 3: Run operator tests — expect failures in two files**

Run: `pnpm --dir src/web test --run features/operator`
Expected:
- `OperatorLayout.test.tsx` still PASSES (the file `@/components/layout/OperatorLayout` still exists; we haven't deleted it yet).
- `OperatorFeature.test.tsx` FAILS — it mocks `@/components/layout/OperatorLayout` and asserts on `data-testid="operator-layout"`, but `OperatorFeature` no longer renders that mock; it now renders `<OperatorShell>`.
- All other operator tests (`CoursePortfolio`, `WalkUpWaitlist`, `TeeSheet`, etc.) PASS — they mount their pages directly via `render(<X />)`, which goes through `TestAppShellProvider`, not through `OperatorFeature`.

This is expected and gets fixed in Task 4. Do not commit yet — Task 4 fixes the broken test.

---

## Task 4: Update `OperatorFeature.test.tsx` to drop deleted layout mocks

**Files:**
- Modify: `src/web/src/features/operator/__tests__/OperatorFeature.test.tsx`

The test mocks `@/components/layout/OperatorLayout` and asserts on a `data-testid="operator-layout"` element that no longer renders. Replace with assertions that work against the new `OperatorShell` flow.

- [ ] **Step 1: Read the current file**

Read `src/web/src/features/operator/__tests__/OperatorFeature.test.tsx`. Current behavior assertions are:
1. When `useFeature('full-operator-app')` returns `false`, navigating to `/operator/waitlist` renders the (mocked) `WalkUpWaitlist` and does NOT render Tee Sheet or Settings.
2. When `useFeature('full-operator-app')` returns `true`, navigating to `/operator/tee-sheet` renders the operator-layout-mounted route group.

These behaviors still exist after Task 3 — only the assertion targets change. Both branches now mount via `<OperatorShell>` which renders `<AppShell>` which calls real shadcn Sidebar components and `useAuth`. We need to mock `AppShell` itself instead of mocking the deleted layouts.

- [ ] **Step 2: Replace the file contents**

Overwrite `src/web/src/features/operator/__tests__/OperatorFeature.test.tsx` with:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorFeature from '../index';

vi.mock('@/hooks/use-features', () => ({
  useFeature: vi.fn(),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: vi.fn(() => ({
    user: { displayName: 'Test User', email: 'test@test.com', organization: { name: 'Test Org' }, role: 'Operator', courses: [{ id: 'course-1', name: 'Test Course' }] },
    logout: vi.fn(),
    courses: [{ id: 'course-1', name: 'Test Course' }],
    organizations: [],
  })),
}));

vi.mock('@/features/auth', async () => {
  const real = await vi.importActual<typeof import('@/features/auth')>('@/features/auth');
  return {
    ...real,
    useAuth: vi.fn(() => ({
      user: { displayName: 'Test User', email: 'test@test.com', organization: { name: 'Test Org' }, role: 'Operator', courses: [{ id: 'course-1', name: 'Test Course' }] },
      logout: vi.fn(),
      courses: [{ id: 'course-1', name: 'Test Course' }],
      organizations: [],
    })),
  };
});

vi.mock('../context/CourseContext', () => ({
  CourseProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useCourseContext: vi.fn(() => ({
    course: { id: 'course-1', name: 'Test Course', timeZoneId: 'America/New_York' },
    selectCourse: vi.fn(),
    clearCourse: vi.fn(),
    isDirty: false,
    registerDirtyForm: vi.fn(),
    unregisterDirtyForm: vi.fn(),
  })),
}));

vi.mock('../context/OrgContext', () => ({
  OrgProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useOrgContext: vi.fn(() => ({
    org: null,
    selectOrg: vi.fn(),
    clearOrg: vi.fn(),
  })),
}));

vi.mock('@/components/ThemeProvider', () => ({
  ThemeProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('../pages/WalkUpWaitlist', () => ({
  default: () => <div data-testid="waitlist-page">Waitlist</div>,
}));

vi.mock('../pages/TeeSheet', () => ({
  default: () => <div data-testid="tee-sheet-page">Tee Sheet</div>,
}));

vi.mock('../pages/TeeTimeSettings', () => ({
  default: () => <div data-testid="settings-page">Settings</div>,
}));

vi.mock('../pages/CoursePortfolio', () => ({
  default: () => <div data-testid="course-portfolio-page">Course Portfolio</div>,
}));

// Mock AppShell (heavy: shadcn Sidebar provider) with a pass-through that exposes
// its variant via data-testid so the routing tests can assert which variant
// the operator feature picked.
vi.mock('@/components/layout/AppShell', () => ({
  AppShell: ({ variant, children }: { variant: 'full' | 'minimal'; children: React.ReactNode }) => (
    <div data-testid={`app-shell-${variant}`}>{children}</div>
  ),
}));

import { useFeature } from '@/hooks/use-features';

const mockUseFeature = vi.mocked(useFeature);

describe('OperatorFeature', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mounts the minimal AppShell variant when full_operator_app is false', () => {
    mockUseFeature.mockReturnValue(false);

    render(<OperatorFeature />, { route: '/operator/waitlist' });

    expect(screen.getByTestId('app-shell-minimal')).toBeInTheDocument();
    expect(screen.getByTestId('waitlist-page')).toBeInTheDocument();
    expect(screen.queryByTestId('tee-sheet-page')).not.toBeInTheDocument();
    expect(screen.queryByTestId('settings-page')).not.toBeInTheDocument();
  });

  it('mounts the full AppShell variant when full_operator_app is true', () => {
    mockUseFeature.mockReturnValue(true);

    render(<OperatorFeature />, { route: '/operator/tee-sheet' });

    expect(screen.getByTestId('app-shell-full')).toBeInTheDocument();
    expect(screen.getByTestId('tee-sheet-page')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run the test**

Run: `pnpm --dir src/web test --run features/operator/__tests__/OperatorFeature.test.tsx`
Expected: 2 tests pass.

- [ ] **Step 4: Run all operator tests to ensure no regressions**

Run: `pnpm --dir src/web test --run features/operator`
Expected: same status as Task 3 — `OperatorLayout.test.tsx` still passes (we have not deleted it yet); `OperatorFeature.test.tsx` now passes; all others pass.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/operator/index.tsx src/web/src/features/operator/__tests__/OperatorFeature.test.tsx
git commit -m "refactor(web): mount operator routes via OperatorShell (cluster 4)"
```

---

## Task 5: Delete the layout shim files

**Files:**
- Delete: `src/web/src/components/layout/OperatorLayout.tsx`
- Delete: `src/web/src/components/layout/WaitlistShellLayout.tsx`

The shims have no consumers after Task 3. `OperatorLayout.test.tsx` still imports `OperatorLayout` — Task 6 fixes that. Run the build between deletion and that fix to confirm only the expected test file breaks.

- [ ] **Step 1: Confirm no production references remain**

Run grep for both file imports:
```bash
grep -rn "@/components/layout/OperatorLayout\|@/components/layout/WaitlistShellLayout" src/web/src --include="*.ts" --include="*.tsx"
```
Expected output: only `src/web/src/features/operator/__tests__/OperatorLayout.test.tsx` references `@/components/layout/OperatorLayout`. Nothing else.

If any other file shows up, STOP and investigate — it's an import we haven't accounted for.

- [ ] **Step 2: Delete the files**

```bash
git rm src/web/src/components/layout/OperatorLayout.tsx
git rm src/web/src/components/layout/WaitlistShellLayout.tsx
```

- [ ] **Step 3: Run the operator test suite — expect ONE file to fail to load**

Run: `pnpm --dir src/web test --run features/operator`
Expected: `OperatorLayout.test.tsx` fails to load because its import of `@/components/layout/OperatorLayout` no longer resolves. All other files pass. Do not commit yet — Task 6 fixes the test file.

---

## Task 6: Rename and re-target `OperatorLayout.test.tsx` → `OperatorBrand.test.tsx`

**Files:**
- Rename: `src/web/src/features/operator/__tests__/OperatorLayout.test.tsx` → `OperatorBrand.test.tsx`
- Modify (after rename): `src/web/src/features/operator/__tests__/OperatorBrand.test.tsx`

The three tests in the file all assert on what is now `<OperatorBrand />` behavior (sidebar header text, fallback to "Teeforce", truncate class on long names). Re-target them at `<OperatorBrand />` directly. No assertion changes — only the mount target.

- [ ] **Step 1: Rename**

```bash
git mv src/web/src/features/operator/__tests__/OperatorLayout.test.tsx \
       src/web/src/features/operator/__tests__/OperatorBrand.test.tsx
```

- [ ] **Step 2: Replace contents**

Overwrite `src/web/src/features/operator/__tests__/OperatorBrand.test.tsx` with:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import { OperatorBrand } from '@/features/operator/navigation';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useOrgContext } from '@/features/operator/context/OrgContext';

vi.mock('@/features/auth/hooks/useAuth');
vi.mock('@/features/operator/context/CourseContext');
vi.mock('@/features/operator/context/OrgContext');

const mockUseAuth = vi.mocked(useAuth);
const mockUseCourseContext = vi.mocked(useCourseContext);
const mockUseOrgContext = vi.mocked(useOrgContext);

describe('OperatorBrand', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseOrgContext.mockReturnValue({
      org: null,
      selectOrg: vi.fn(),
      clearOrg: vi.fn(),
    });
    mockUseCourseContext.mockReturnValue({
      course: null,
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
      isDirty: false,
      registerDirtyForm: vi.fn(),
      unregisterDirtyForm: vi.fn(),
    });
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Operator',
        organization: { id: 'org-1', name: 'Pine Valley Golf Club' },
        organizations: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      unauthorized: false,
      permissions: ['app:access'],
      courses: [],
      organizations: [],
      login: vi.fn(),
      logout: vi.fn(),
      hasPermission: vi.fn(() => true),
    });
  });

  it('shows organization name in sidebar header', () => {
    render(<OperatorBrand />);
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
  });

  it('shows Teeforce when user has no organization', () => {
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Operator',
        organization: null,
        organizations: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      unauthorized: false,
      permissions: ['app:access'],
      courses: [],
      organizations: [],
      login: vi.fn(),
      logout: vi.fn(),
      hasPermission: vi.fn(() => true),
    });

    render(<OperatorBrand />);
    expect(screen.getByText('Teeforce')).toBeInTheDocument();
  });

  it('applies truncate class and title attribute for long organization names', () => {
    const longName = 'Very Long Organization Name That Should Be Truncated';
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Operator',
        organization: { id: 'org-1', name: longName },
        organizations: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      unauthorized: false,
      permissions: ['app:access'],
      courses: [],
      organizations: [],
      login: vi.fn(),
      logout: vi.fn(),
      hasPermission: vi.fn(() => true),
    });

    render(<OperatorBrand />);
    const heading = screen.getByText(longName);
    expect(heading).toHaveClass('truncate');
    expect(heading).toHaveClass('max-w-[180px]');
    expect(heading).toHaveAttribute('title', longName);
  });
});
```

- [ ] **Step 3: Run the test**

Run: `pnpm --dir src/web test --run features/operator/__tests__/OperatorBrand.test.tsx`
Expected: 3 tests pass.

- [ ] **Step 4: Run all operator tests — should all pass now**

Run: `pnpm --dir src/web test --run features/operator`
Expected: every operator test file passes (no test files fail to load, no assertions fail).

- [ ] **Step 5: Run the full web test suite for a sanity check**

Run: `pnpm --dir src/web test --run`
Expected: 215 tests pass (or whatever the new total is, accounting for the 3 renamed tests counted under their new file).

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/operator/__tests__/OperatorBrand.test.tsx \
        src/web/src/components/layout/OperatorLayout.tsx \
        src/web/src/components/layout/WaitlistShellLayout.tsx
git commit -m "refactor(web): delete OperatorLayout/WaitlistShellLayout shims (cluster 4)"
```

(The two `git rm` deletions from Task 5 are still staged; this commit bundles them with the test rename.)

---

## Task 7: Restyle `OrgPicker.tsx`

**Files:**
- Modify: `src/web/src/features/operator/pages/OrgPicker.tsx`

Smallest of the three pages. No tests target it directly. Drop the outer `p-6`, move the title to `<PageTopbar>`, apply Fieldstone Card treatment (`border-border-strong`, `hover:bg-canvas`).

- [ ] **Step 1: Replace the file contents**

Overwrite `src/web/src/features/operator/pages/OrgPicker.tsx` with:

```tsx
import { useAuth } from '@/features/auth';
import { useOrgContext } from '../context/OrgContext';
import { Card, CardContent } from '@/components/ui/card';
import { PageTopbar } from '@/components/layout/PageTopbar';

export default function OrgPicker() {
  const { organizations } = useAuth();
  const { selectOrg } = useOrgContext();

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
}
```

- [ ] **Step 2: Verify lint and tests still pass**

Run: `pnpm --dir src/web lint`
Expected: 0 errors.

Run: `pnpm --dir src/web test --run features/operator`
Expected: all operator tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/pages/OrgPicker.tsx
git commit -m "feat(web): restyle OrgPicker for Fieldstone (cluster 4)"
```

---

## Task 8: Restyle `CoursePortfolio.tsx` and update its tests

**Files:**
- Modify: `src/web/src/features/operator/pages/CoursePortfolio.tsx`
- Modify: `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx`

This task changes two test assertions deliberately. Both changes follow the deliberate UI changes in the spec:

1. The empty-state copy is consolidated from two paragraphs ("No courses available" + "Contact your administrator to add a course.") to one line ("No courses available. Contact your administrator to add a course."). The existing assertion `getByText('Contact your administrator to add a course.')` no longer matches as exact text.
2. The decorative `Manage` button is removed from each Card (the entire card is already `role="button"` with an aria-label). The existing assertion `getAllByText('Manage', { selector: 'button' })` no longer finds anything.

PR justification, copy verbatim: *"CoursePortfolio test assertions updated for two deliberate UI changes: empty-state copy consolidated to one line per Cluster 4 spec; decorative `Manage` button removed (cards are already clickable via `role="button"` and aria-label). Both changes preserve all interactive behavior — only visual chrome is reduced. The keyboard navigation, click handlers, and accessible names are unchanged."*

- [ ] **Step 1: Replace `CoursePortfolio.tsx`**

Overwrite `src/web/src/features/operator/pages/CoursePortfolio.tsx` with:

```tsx
import { useEffect, useRef } from 'react';
import { useCourses } from '@/features/operator/hooks/useCourses';
import { useCourseContext } from '../context/CourseContext';
import { Card, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { PageTopbar } from '@/components/layout/PageTopbar';
import type { Course } from '@/types/course';

function formatLocation(course: Course): string {
  const parts = [course.city, course.state].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : 'Location not set';
}

export default function CoursePortfolio() {
  const { selectCourse } = useCourseContext();
  const coursesQuery = useCourses();
  const hasAutoSelected = useRef(false);

  useEffect(() => {
    if (
      !coursesQuery.isLoading &&
      coursesQuery.data &&
      coursesQuery.data.length === 1 &&
      !hasAutoSelected.current
    ) {
      hasAutoSelected.current = true;
      const course = coursesQuery.data[0];
      if (course) {
        selectCourse({ id: course.id, name: course.name, timeZoneId: course.timeZoneId });
      }
    }
  }, [coursesQuery.isLoading, coursesQuery.data, selectCourse]);

  function handleSelectCourse(course: Course) {
    selectCourse({ id: course.id, name: course.name, timeZoneId: course.timeZoneId });
  }

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
}
```

- [ ] **Step 2: Run CoursePortfolio tests — expect 2 failures**

Run: `pnpm --dir src/web test --run features/operator/__tests__/CoursePortfolio.test.tsx`
Expected:
- `'shows admin contact message when no courses registered'` FAILS — `getByText('Contact your administrator to add a course.')` does not match (consolidated copy).
- `'renders multiple course cards'` FAILS — `getAllByText('Manage', { selector: 'button' })` returns 0 elements (button removed).
- All other tests in the file PASS (loading state, error state, auto-select single course, click handler, keydown handlers, aria-labels).

- [ ] **Step 3: Update the failing assertions**

Edit `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx`.

In the test `'shows admin contact message when no courses registered'`, replace these two lines:

```tsx
expect(screen.getByText('No courses available')).toBeInTheDocument();
expect(screen.getByText('Contact your administrator to add a course.')).toBeInTheDocument();
```

with:

```tsx
expect(
  screen.getByText('No courses available. Contact your administrator to add a course.')
).toBeInTheDocument();
```

In the test `'renders multiple course cards'`, **delete this line** entirely (do not replace):

```tsx
// "Manage" buttons are aria-hidden (decorative), use getAllByText with hidden:true
expect(screen.getAllByText('Manage', { selector: 'button' })).toHaveLength(2);
```

The cards' interactive identity is still asserted by the click and keyboard tests below, which use `getByLabelText('Manage Pine Valley, Augusta, GA')` — those still pass because the aria-label survives. The deleted assertion specifically tested the decorative button visual, not the card behavior.

- [ ] **Step 4: Re-run CoursePortfolio tests**

Run: `pnpm --dir src/web test --run features/operator/__tests__/CoursePortfolio.test.tsx`
Expected: all tests pass.

- [ ] **Step 5: Run the full operator suite for a sanity check**

Run: `pnpm --dir src/web test --run features/operator`
Expected: all operator tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/operator/pages/CoursePortfolio.tsx \
        src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx
git commit -m "feat(web): restyle CoursePortfolio for Fieldstone (cluster 4)

Two test assertion updates per spec:
- Empty-state copy consolidated to one line
- Decorative Manage button removed (cards remain clickable via aria-label)

All keyboard navigation and click handler behavior is preserved."
```

---

## Task 9: Restyle `TeeTimeSettings.tsx`

**Files:**
- Modify: `src/web/src/features/operator/pages/TeeTimeSettings.tsx`

Wrap the form in a Fieldstone Card with an uppercase-tracked title, move the page title to `<PageTopbar>`, sweep `text-success` → `text-green`, restyle the empty state.

- [ ] **Step 1: Replace the file contents**

Overwrite `src/web/src/features/operator/pages/TeeTimeSettings.tsx` with:

```tsx
import { useEffect } from 'react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  useTeeTimeSettings,
  useUpdateTeeTimeSettings,
} from '../hooks/useTeeTimeSettings';
import { useCourseContext } from '../context/CourseContext';

const teeTimeSettingsSchema = z.object({
  teeTimeIntervalMinutes: z.number().refine((val) => [8, 10, 12].includes(val), {
    message: 'Interval must be 8, 10, or 12 minutes',
  }),
  firstTeeTime: z.string().min(1, 'First tee time is required'),
  lastTeeTime: z.string().min(1, 'Last tee time is required'),
});

type TeeTimeSettingsFormData = z.infer<typeof teeTimeSettingsSchema>;

export default function TeeTimeSettings() {
  const { course, registerDirtyForm, unregisterDirtyForm } = useCourseContext();

  const form = useForm<TeeTimeSettingsFormData>({
    resolver: zodResolver(teeTimeSettingsSchema),
    defaultValues: {
      teeTimeIntervalMinutes: 10,
      firstTeeTime: '07:00',
      lastTeeTime: '18:00',
    },
  });

  const settingsQuery = useTeeTimeSettings(course?.id);
  const updateMutation = useUpdateTeeTimeSettings();

  const formIsDirty = form.formState.isDirty;

  useEffect(() => {
    if (formIsDirty) {
      registerDirtyForm('tee-time-settings');
    } else {
      unregisterDirtyForm('tee-time-settings');
    }
    return () => {
      unregisterDirtyForm('tee-time-settings');
    };
  }, [formIsDirty, registerDirtyForm, unregisterDirtyForm]);

  useEffect(() => {
    if (settingsQuery.data?.firstTeeTime && settingsQuery.data.lastTeeTime) {
      form.reset({
        teeTimeIntervalMinutes: settingsQuery.data.teeTimeIntervalMinutes,
        firstTeeTime: settingsQuery.data.firstTeeTime.slice(0, 5),
        lastTeeTime: settingsQuery.data.lastTeeTime.slice(0, 5),
      });
    }
  }, [settingsQuery.data, form]);

  const courseId = course?.id;

  function onSubmit(data: TeeTimeSettingsFormData) {
    if (!courseId) return;
    updateMutation.mutate({ courseId, data });
  }

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

                  <FormField
                    control={form.control}
                    name="teeTimeIntervalMinutes"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tee Time Interval</FormLabel>
                        <Select
                          value={String(field.value)}
                          onValueChange={(value) => field.onChange(Number(value))}
                        >
                          <FormControl>
                            <SelectTrigger>
                              <SelectValue placeholder="Select interval" />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            <SelectItem value="8">Every 8 minutes</SelectItem>
                            <SelectItem value="10">Every 10 minutes</SelectItem>
                            <SelectItem value="12">Every 12 minutes</SelectItem>
                          </SelectContent>
                        </Select>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      control={form.control}
                      name="firstTeeTime"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>First Tee Time</FormLabel>
                          <FormControl>
                            <Input type="time" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    <FormField
                      control={form.control}
                      name="lastTeeTime"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Last Tee Time</FormLabel>
                          <FormControl>
                            <Input type="time" {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>

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
}
```

Notes on the changes:
- Outer `<div className="p-6 max-w-2xl">` removed; `max-w-2xl` moved to the inner form-wrapping div (only present when `course` is set, so the empty state is full-width centered).
- `<PageHeader title="Tee Time Settings" />` removed; title moves to `<PageTopbar>`.
- Form wrapped in `<Card border-border-strong>` with uppercase-tracked `<CardTitle>` ("Tee Time Configuration").
- Loading and error messages moved inside the form's `space-y-6` block (above the first field).
- `text-success` swept to `text-green` (Fieldstone token).
- The `if (!course)` early return became a ternary inside the JSX so the topbar still renders during the empty state.
- The local `courseId` const is kept (now optional) and the submit handler guards on it; this preserves the existing typed `mutate` call.

- [ ] **Step 2: Verify lint and tests**

Run: `pnpm --dir src/web lint`
Expected: 0 errors.

Run: `pnpm --dir src/web test --run features/operator`
Expected: all operator tests pass. (No test file targets `TeeTimeSettings` directly.)

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/pages/TeeTimeSettings.tsx
git commit -m "feat(web): restyle TeeTimeSettings for Fieldstone (cluster 4)"
```

---

## Task 10: Full verification — lint, test, build

**Files:** none — verification only.

- [ ] **Step 1: Lint**

Run: `pnpm --dir src/web lint`
Expected: 0 errors. Note any pre-existing warnings (do not address — out of scope).

- [ ] **Step 2: Type-check + test**

Run: `pnpm --dir src/web test --run`
Expected: full suite passes. The total count should be the same as the baseline (215 tests) — no tests added or removed (the OperatorLayout → OperatorBrand rename keeps all 3 tests; the CoursePortfolio test deletion removes 1 assertion, not a whole test).

- [ ] **Step 3: Production build**

Run: `pnpm --dir src/web build`
Expected: build succeeds with no errors.

- [ ] **Step 4: Confirm shim files are gone**

Run:
```bash
ls src/web/src/components/layout/
```
Expected output (alphabetically):
```
AppShell.tsx
AppShellContext.tsx
PageHeader.tsx
PageRightRail.tsx
PageTopbar.tsx
UserMenu.tsx
```
There should be NO `OperatorLayout.tsx` and NO `WaitlistShellLayout.tsx`.

(If `UserMenu.tsx` is not in `layout/`, that's fine — its actual location was confirmed during planning to be `components/layout/`. If it's elsewhere, the expected output above just lists `AppShell.tsx`, `AppShellContext.tsx`, `PageHeader.tsx`, `PageRightRail.tsx`, `PageTopbar.tsx`.)

- [ ] **Step 5: Confirm no stray references**

Run:
```bash
grep -rn "OperatorLayout\|WaitlistShellLayout" src/web/src --include="*.ts" --include="*.tsx"
```
Expected: no matches.

---

## Task 11: Manual smoke via `make dev`

**Files:** none — manual verification per project rule.

`make dev` is mandatory before declaring backend or frontend work complete (per CLAUDE.md). The user will exercise the flows below; you confirm them before moving on.

- [ ] **Step 1: Start the dev environment**

From the worktree root:

```bash
make dev
```

API on `:5221`, web on `:3000`. Wait for both to come up. (If you are running this in a sub-agent without TTY, ask the user to run `make dev` and exercise the flows below.)

- [ ] **Step 2: Smoke each operator surface**

Visit each of the following routes and confirm there are no console errors and the page renders the Fieldstone visual treatment correctly:

| URL | Auth state | Expected page | Verify |
|---|---|---|---|
| `/operator` | Admin, no org selected | `OrgPicker` in **full** AppShell | Topbar title "Select an Organization" in serif; sidebar nav present (Operations / Management); org tiles in `border-border-strong` Cards with `hover:bg-canvas` |
| `/operator` | Admin, org selected, multiple courses, `full-operator-app=true` | `CoursePortfolio` in **full** AppShell | Topbar title "Select a Course" in serif; left-aligned `max-w-3xl` column; no centered hero; no decorative `Manage` button on cards; `border-border-strong` + `hover:bg-canvas` |
| `/operator` | Operator, no course selected, `full-operator-app=false` | `CoursePortfolio` in **minimal** AppShell | Same Card treatment as above, but shell is minimal — no sidebar; topbar shows `WaitlistBrand` (course name fallback) |
| `/operator/settings` | Course selected | `TeeTimeSettings` form in Card | Topbar title in serif; form wrapped in `border-border-strong` Card with uppercase-tracked "Tee Time Configuration" header; success message renders in Fieldstone green (not the old `text-success`) |
| `/operator/settings` | No course selected | Empty-state message | Topbar title still visible; centered muted message reads "Select a course from the sidebar to configure settings."; no Card chrome around the message |
| `/operator/tee-sheet` | Course selected, `full-operator-app=true` | `TeeSheet` (cluster 0/foundation) | Unchanged from main; sanity check that the new `OperatorShell` mount didn't regress this page |
| `/operator/waitlist` | Course selected, `full-operator-app=true` | `WalkUpWaitlist` (cluster 1) | Unchanged from main; sanity check |
| `/operator/waitlist` | Course selected, `full-operator-app=false` | `WalkUpWaitlist` in **minimal** shell (cluster 1 page in cluster 4 shell) | Confirms the second `WaitlistShellLayout` consumer is correctly inlined |
| `/admin/organizations` | Admin | `OrgList` (cluster 2) | Unchanged from main; sanity check that no cross-feature regression slipped in |
| One golfer page (e.g. `/walkup` if dev seed allows) | Any | Golfer page | Sanity check that no token cascade affected golfer flows |

For each row, the user confirms with a thumbs-up. If anything looks broken visually, capture a screenshot and post a fix-up commit before opening the PR.

- [ ] **Step 3: Stop the dev environment**

```bash
make down
```

- [ ] **Step 4: Confirm clean working tree**

Run: `git status`
Expected: clean working tree, all commits made.

---

## Task 12: Open the PR

**Files:** none.

- [ ] **Step 1: Push the branch**

```bash
git push -u origin chore/fieldstone-cluster-4-operator-longtail
```

- [ ] **Step 2: Capture before/after screenshots**

For each of the three pages (`OrgPicker`, `CoursePortfolio` in both full and minimal variants, `TeeTimeSettings`), capture a before screenshot from `main` and an after screenshot from this branch. Store them locally for the PR description.

- [ ] **Step 3: Open the PR**

```bash
gh pr create --title "feat(web): Fieldstone redesign — operator long-tail (Cluster 4)" --body "$(cat <<'EOF'
## Summary

Closes #385. Final cluster of the Fieldstone redesign rollout (#381). Restyles the three operator long-tail pages and removes both remaining layout shims.

- **Pages restyled:** `CoursePortfolio`, `OrgPicker`, `TeeTimeSettings`
- **Shims removed:** `OperatorLayout.tsx` AND `WaitlistShellLayout.tsx` — both deleted in this PR
- **New:** `useOperatorShellProps` hook + 5-line local `OperatorShell` wrapper in `features/operator/index.tsx`
- **Brand colocation:** `OperatorBrand`, `WaitlistBrand`, and the interactive `OrgSwitcher` lifted into `features/operator/navigation.tsx` (renamed from `.ts`), matching the Cluster 2 admin precedent

## Scope correction

The #385 issue text claims `WaitlistShellLayout`'s only remaining consumer after Cluster 1 is `CoursePortfolio`. That is incorrect — Cluster 1 restyled `WalkUpWaitlist`'s contents but did not touch its mounting, so `WaitlistShellLayout` still wrapped both `CoursePortfolio` and `WalkUpWaitlist` in the `!fullOperatorApp` branches. Deleting the file required fixing **two** mount sites, not one. The restyled `WalkUpWaitlist` content from Cluster 1 is unchanged; only its shell mount moves.

## Test changes

Two test files have assertion-level changes documented as required by the test-integrity rule:

1. **`OperatorLayout.test.tsx` → `OperatorBrand.test.tsx`** — file renamed via `git mv`. Same 3 assertions, re-targeted at `<OperatorBrand />` instead of `<OperatorLayout />`. Behavior assertions are unchanged; only the mount target moved.

2. **`CoursePortfolio.test.tsx`** — two assertion changes:
   - Empty-state copy was consolidated from two paragraphs to one line per spec; the assertion now matches the consolidated string.
   - The decorative `Manage` button was removed from each Card (the cards are still `role="button"` with full aria-labels and click/keyboard handlers); the assertion that asserted the button's presence was deleted. All interactive behavior assertions (click, Enter, Space, aria-label lookup) are preserved.

3. **`OperatorFeature.test.tsx`** — mocks updated to mock `@/components/layout/AppShell` (with a pass-through that exposes the variant via `data-testid`) instead of mocking the deleted layouts. Both behavior assertions ("minimal variant when full_operator_app is false" and "full variant when full_operator_app is true") are preserved with updated targets.

No new tests added (per cluster rule).

## Spec

[`docs/superpowers/specs/2026-04-08-operator-longtail-cluster-4-design.md`](docs/superpowers/specs/2026-04-08-operator-longtail-cluster-4-design.md)

## Test plan

- [ ] `pnpm --dir src/web lint` clean
- [ ] `pnpm --dir src/web test --run` clean
- [ ] `pnpm --dir src/web build` clean
- [ ] Manual smoke: `/operator` as admin (OrgPicker), `/operator` with `full-operator-app=true` and multiple courses (CoursePortfolio in full shell), `/operator` with `full-operator-app=false` (CoursePortfolio in minimal shell), `/operator/settings` with and without a course, `/operator/tee-sheet`, `/operator/waitlist`, one admin page, one golfer page

## Before/after screenshots

[Inline screenshots here]

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Return the PR URL to the user**

---

## Self-review checklist

Run through this before marking the plan complete:

- [x] **Spec coverage:** Every section of the spec maps to a task. Section 1 (architecture, shell removal, hook, mount sites) → Tasks 1, 2, 3, 5. Section 2 (brand colocation) → Task 1. Section 3 (CoursePortfolio restyle) → Task 8. Section 4 (OrgPicker restyle) → Task 7. Section 5 (TeeTimeSettings restyle) → Task 9. Section 6 (tests) → Tasks 4, 6, 8. Section 7 (files summary) → all tasks. Section 8 (risks) → addressed via test updates and verification tasks. Done criteria → Tasks 10–12.
- [x] **No placeholders:** every code block is complete; no "TBD", no "fill in", no "similar to task N".
- [x] **Type consistency:** the hook signature `useOperatorShellProps(variant: Variant): ShellProps` is consistent across Tasks 2 and 3. The `OperatorShell` wrapper accepts `{ variant, children }` consistently. The `OperatorBrand` / `WaitlistBrand` exports are referenced consistently across Tasks 1, 2, and 6.
- [x] **Order matters:** Task 1 (brand exports) → Task 2 (hook that imports them) → Task 3 (index that uses the hook, expects ONE test failure) → Task 4 (fix that test, commit Task 3+4 together) → Task 5 (delete shims, expect ONE test failure) → Task 6 (fix that test, commit Task 5+6 together) → Tasks 7, 8, 9 (page restyles, can be done in any order but Task 8 has the test changes so it's listed specifically) → Tasks 10, 11, 12 (verify, smoke, ship).
- [x] **Test integrity rule respected:** the only assertion changes are documented in Task 8 (CoursePortfolio) with PR justification copy provided verbatim. All other test changes are scaffolding (rename, mount target swap, mock target swap), not assertion changes.
- [x] **Frequent commits:** every task ends with a commit. Tasks 3+4 and Tasks 5+6 produce one commit each because the intermediate state is intentionally broken — the fix is bundled with the change that breaks it.
- [x] **No new files outside the spec:** the hook (Task 2) and the renamed test (Task 6) are the only structural additions; both are listed in the spec's "Files created/modified" section.
