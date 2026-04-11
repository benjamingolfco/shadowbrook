# Remove Legacy Operator Feature — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete `features/operator/`, move OrgContext and useCourses to the course feature, add a CourseProvider with real timezone data, wire up feature flag routing and dirty form detection, and build the course picker page.

**Architecture:** The operator feature is a legacy parallel implementation. The course feature already has all the POS and management pages; it just needs the missing pieces (OrgContext, course picker, CourseProvider with timezone, feature flag routing, dirty form warning). Once those gaps are filled, the operator feature is deleted and the router is updated so `/course` is the entry point for operators.

**Tech Stack:** React 19, TypeScript 5.9, React Router 7, TanStack Query, React Hook Form + Zod, shadcn/ui, Vitest + RTL

---

## File Structure

### Files to Create

| File | Purpose |
|------|---------|
| `src/web/src/features/course/context/OrgContext.tsx` | Admin org impersonation context (moved from operator, identical) |
| `src/web/src/features/course/hooks/useCourses.ts` | Course listing hook (moved from operator, identical) |
| `src/web/src/features/course/hooks/useCourse.ts` | Single course metadata hook (GET `/courses/:courseId`) |
| `src/web/src/features/course/context/CourseProvider.tsx` | Route-driven provider: fetches course by courseId param, exposes `timeZoneId` + `name` via context |
| `src/web/src/features/course/pages/CoursePicker.tsx` | Course selection page with admin org gate |
| `src/web/src/features/course/__tests__/CourseProvider.test.tsx` | Tests for CourseProvider context |
| `src/web/src/features/course/__tests__/CoursePicker.test.tsx` | Tests for CoursePicker page |
| `src/web/src/features/course/__tests__/Settings.test.tsx` | Tests for dirty form detection on Settings |

### Files to Modify

| File | Change |
|------|--------|
| `src/web/src/features/course/index.tsx` | Add feature flag routing, OrgProvider wrap, CourseProvider wrap, course picker route |
| `src/web/src/app/router.tsx` | Remove operator route, remove `/course` redirect, update RoleRedirect |
| `src/web/src/features/course/pos/pages/TeeSheet.tsx` | Replace `getBrowserTimeZone()` with course timezone from context |
| `src/web/src/features/course/pos/pages/WalkUpWaitlist.tsx` | Replace `getBrowserTimeZone()` with course timezone from context |
| `src/web/src/features/course/pos/components/PostTeeTimeForm.tsx` | Replace `getBrowserTimeZone()` with course timezone from context |
| `src/web/src/features/course/pos/components/AddTeeTimeOpeningDialog.tsx` | Replace `getBrowserTimeZone()` with course timezone from context |
| `src/web/src/features/course/manage/pages/Settings.tsx` | Add dirty form detection with `useBlocker` |
| `src/web/src/features/course/pos/layouts/PosLayout.tsx` | Accept optional `variant` prop for minimal shell |

### Files to Delete

| Path | Notes |
|------|-------|
| `src/web/src/features/operator/` (entire directory) | All 46 files — pages, components, hooks, contexts, tests, navigation |

---

## Task 1: Move OrgContext to Course Feature

**Files:**
- Create: `src/web/src/features/course/context/OrgContext.tsx`

- [ ] **Step 1: Create the OrgContext file**

Copy the operator's OrgContext wholesale — same implementation, new location.

```tsx
// src/web/src/features/course/context/OrgContext.tsx
import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setAdminOrgIdGetter } from '@/lib/api-client';

export interface SelectedOrg {
  id: string;
  name: string;
}

interface OrgContextValue {
  org: SelectedOrg | null;
  selectOrg: (org: SelectedOrg) => void;
  clearOrg: () => void;
}

const OrgContext = createContext<OrgContextValue | undefined>(undefined);

const STORAGE_KEY = 'teeforce-admin-org';

interface OrgProviderProps {
  children: ReactNode;
}

export function OrgProvider({ children }: OrgProviderProps) {
  const queryClient = useQueryClient();
  const [org, setOrg] = useState<SelectedOrg | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as SelectedOrg;
    } catch {
      return null;
    }
  });

  useEffect(() => {
    setAdminOrgIdGetter(() => org?.id ?? null);
    return () => setAdminOrgIdGetter(() => null);
  }, [org]);

  const selectOrg = useCallback((newOrg: SelectedOrg) => {
    setOrg(newOrg);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(newOrg));
    void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
  }, [queryClient]);

  const clearOrg = useCallback(() => {
    setOrg(null);
    localStorage.removeItem(STORAGE_KEY);
    void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
  }, [queryClient]);

  return (
    <OrgContext.Provider value={{ org, selectOrg, clearOrg }}>
      {children}
    </OrgContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useOrgContext() {
  const context = useContext(OrgContext);
  if (context === undefined) {
    throw new Error('useOrgContext must be used within an OrgProvider');
  }
  return context;
}
```

- [ ] **Step 2: Verify no lint errors**

Run: `pnpm --dir src/web lint`
Expected: No errors related to `features/course/context/OrgContext.tsx`

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/course/context/OrgContext.tsx
git commit -m "feat: move OrgContext to course feature"
```

---

## Task 2: Move useCourses Hook to Course Feature

**Files:**
- Create: `src/web/src/features/course/hooks/useCourses.ts`

- [ ] **Step 1: Create the useCourses hook**

```ts
// src/web/src/features/course/hooks/useCourses.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Course } from '@/types/course';

export function useCourses() {
  return useQuery({
    queryKey: queryKeys.courses.all,
    queryFn: () => api.get<Course[]>('/courses'),
  });
}
```

- [ ] **Step 2: Verify no lint errors**

Run: `pnpm --dir src/web lint`
Expected: No errors related to `features/course/hooks/useCourses.ts`

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/course/hooks/useCourses.ts
git commit -m "feat: move useCourses hook to course feature"
```

---

## Task 3: Create useCourse Hook and CourseProvider

**Files:**
- Create: `src/web/src/features/course/hooks/useCourse.ts`
- Create: `src/web/src/features/course/context/CourseProvider.tsx`
- Create: `src/web/src/features/course/__tests__/CourseProvider.test.tsx`

- [ ] **Step 1: Write the test for CourseProvider**

```tsx
// src/web/src/features/course/__tests__/CourseProvider.test.tsx
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { type ReactNode } from 'react';
import { useCourseContext } from '../context/CourseProvider';

vi.mock('@/lib/api-client', () => ({
  api: {
    get: vi.fn(),
  },
}));

import { api } from '@/lib/api-client';

function createWrapper(courseId: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[`/course/${courseId}/manage`]}>
          <Routes>
            <Route path="/course/:courseId/*" element={children} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
  };
}

describe('CourseProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('provides course metadata after fetch', async () => {
    vi.mocked(api.get).mockResolvedValueOnce({
      id: 'course-1',
      name: 'Pine Valley',
      timeZoneId: 'America/New_York',
    });

    const { result } = renderHook(() => useCourseContext(), {
      wrapper: createWrapper('course-1'),
    });

    await waitFor(() => {
      expect(result.current.course).not.toBeNull();
    });

    expect(result.current.course!.name).toBe('Pine Valley');
    expect(result.current.course!.timeZoneId).toBe('America/New_York');
  });

  it('returns loading true while fetching', () => {
    vi.mocked(api.get).mockReturnValue(new Promise(() => {})); // never resolves

    const { result } = renderHook(() => useCourseContext(), {
      wrapper: createWrapper('course-1'),
    });

    expect(result.current.isLoading).toBe(true);
    expect(result.current.course).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm --dir src/web vitest run src/web/src/features/course/__tests__/CourseProvider.test.tsx`
Expected: FAIL — `useCourseContext` module not found

- [ ] **Step 3: Create useCourse hook**

```ts
// src/web/src/features/course/hooks/useCourse.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Course } from '@/types/course';

export function useCourse(courseId: string) {
  return useQuery({
    queryKey: queryKeys.courses.detail(courseId),
    queryFn: () => api.get<Course>(`/courses/${courseId}`),
  });
}
```

- [ ] **Step 4: Create CourseProvider**

```tsx
// src/web/src/features/course/context/CourseProvider.tsx
import { createContext, useContext, type ReactNode } from 'react';
import { useCourseId } from '../hooks/useCourseId';
import { useCourse } from '../hooks/useCourse';

interface CourseContextValue {
  course: { id: string; name: string; timeZoneId: string } | null;
  isLoading: boolean;
}

const CourseContext = createContext<CourseContextValue | undefined>(undefined);

interface CourseProviderProps {
  children: ReactNode;
}

export function CourseProvider({ children }: CourseProviderProps) {
  const courseId = useCourseId();
  const { data, isLoading } = useCourse(courseId);

  const course = data
    ? { id: data.id, name: data.name, timeZoneId: data.timeZoneId }
    : null;

  return (
    <CourseContext.Provider value={{ course, isLoading }}>
      {children}
    </CourseContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCourseContext() {
  const context = useContext(CourseContext);
  if (context === undefined) {
    throw new Error('useCourseContext must be used within a CourseProvider');
  }
  return context;
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `pnpm --dir src/web vitest run src/web/src/features/course/__tests__/CourseProvider.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/course/hooks/useCourse.ts src/web/src/features/course/context/CourseProvider.tsx src/web/src/features/course/__tests__/CourseProvider.test.tsx
git commit -m "feat: add CourseProvider with route-driven course metadata"
```

---

## Task 4: Replace getBrowserTimeZone with Course Timezone

**Files:**
- Modify: `src/web/src/features/course/pos/pages/TeeSheet.tsx`
- Modify: `src/web/src/features/course/pos/pages/WalkUpWaitlist.tsx`
- Modify: `src/web/src/features/course/pos/components/PostTeeTimeForm.tsx`
- Modify: `src/web/src/features/course/pos/components/AddTeeTimeOpeningDialog.tsx`

- [ ] **Step 1: Update TeeSheet to use course timezone**

In `src/web/src/features/course/pos/pages/TeeSheet.tsx`:

Replace the import:
```tsx
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';
```
with:
```tsx
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';
import { useCourseContext } from '../../context/CourseProvider';
```

Replace the timezone line:
```tsx
const timeZone = getBrowserTimeZone();
```
with:
```tsx
const { course } = useCourseContext();
const timeZone = course?.timeZoneId ?? getBrowserTimeZone();
```

- [ ] **Step 2: Update WalkUpWaitlist to use course timezone**

In `src/web/src/features/course/pos/pages/WalkUpWaitlist.tsx`:

Add import:
```tsx
import { useCourseContext } from '../../context/CourseProvider';
```

Replace:
```tsx
const timeZoneId = getBrowserTimeZone();
```
with:
```tsx
const { course } = useCourseContext();
const timeZoneId = course?.timeZoneId ?? getBrowserTimeZone();
```

- [ ] **Step 3: Update PostTeeTimeForm to use course timezone**

In `src/web/src/features/course/pos/components/PostTeeTimeForm.tsx`:

Add import:
```tsx
import { useCourseContext } from '../../context/CourseProvider';
```

Replace:
```tsx
const timeZoneId = getBrowserTimeZone();
```
with:
```tsx
const { course } = useCourseContext();
const timeZoneId = course?.timeZoneId ?? getBrowserTimeZone();
```

- [ ] **Step 4: Update AddTeeTimeOpeningDialog to use course timezone**

In `src/web/src/features/course/pos/components/AddTeeTimeOpeningDialog.tsx`:

Add import:
```tsx
import { useCourseContext } from '../../context/CourseProvider';
```

Replace:
```tsx
const timeZoneId = getBrowserTimeZone();
```
with:
```tsx
const { course } = useCourseContext();
const timeZoneId = course?.timeZoneId ?? getBrowserTimeZone();
```

- [ ] **Step 5: Verify lint passes**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/course/pos/pages/TeeSheet.tsx src/web/src/features/course/pos/pages/WalkUpWaitlist.tsx src/web/src/features/course/pos/components/PostTeeTimeForm.tsx src/web/src/features/course/pos/components/AddTeeTimeOpeningDialog.tsx
git commit -m "fix: use course timezone instead of browser timezone in POS pages"
```

---

## Task 5: Add Dirty Form Detection to Settings

**Files:**
- Modify: `src/web/src/features/course/manage/pages/Settings.tsx`
- Create: `src/web/src/features/course/__tests__/Settings.test.tsx`

- [ ] **Step 1: Write the test for dirty form blocking**

```tsx
// src/web/src/features/course/__tests__/Settings.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryRouter, RouterProvider } from 'react-router';
import Settings from '../manage/pages/Settings';

vi.mock('../../hooks/useCourseId', () => ({
  useCourseId: () => 'course-1',
}));

vi.mock('../manage/hooks/useTeeTimeSettings', () => ({
  useTeeTimeSettings: vi.fn(() => ({
    data: {
      teeTimeIntervalMinutes: 10,
      firstTeeTime: '07:00:00',
      lastTeeTime: '18:00:00',
      defaultCapacity: 4,
    },
    isLoading: false,
    isError: false,
  })),
  useUpdateTeeTimeSettings: vi.fn(() => ({
    mutate: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
  })),
}));

function renderWithRouter() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  const router = createMemoryRouter(
    [
      { path: '/course/:courseId/manage/settings', element: <Settings /> },
      { path: '/course/:courseId/manage', element: <div>Dashboard</div> },
    ],
    { initialEntries: ['/course/course-1/manage/settings'] },
  );

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe('Settings dirty form detection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the settings form', () => {
    renderWithRouter();
    expect(screen.getByText('Tee Time Settings')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it passes**

Run: `pnpm --dir src/web vitest run src/web/src/features/course/__tests__/Settings.test.tsx`
Expected: PASS

- [ ] **Step 3: Add useBlocker to Settings page**

In `src/web/src/features/course/manage/pages/Settings.tsx`:

Add import at the top:
```tsx
import { useBlocker } from 'react-router';
```

After the `form` declaration (around line 36), add:
```tsx
const isDirty = form.formState.isDirty;

useBlocker(({ currentLocation, nextLocation }) =>
  isDirty && currentLocation.pathname !== nextLocation.pathname,
);
```

Add a visual indicator below the `<PageTopbar>` and above the `<Card>`:
```tsx
{isDirty && (
  <div className="mb-4 rounded-md border border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200">
    You have unsaved changes.
  </div>
)}
```

- [ ] **Step 4: Run tests**

Run: `pnpm --dir src/web vitest run src/web/src/features/course/__tests__/Settings.test.tsx`
Expected: PASS

- [ ] **Step 5: Verify lint passes**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/course/manage/pages/Settings.tsx src/web/src/features/course/__tests__/Settings.test.tsx
git commit -m "feat: add dirty form detection to course Settings page"
```

---

## Task 6: Create Course Picker Page

**Files:**
- Create: `src/web/src/features/course/pages/CoursePicker.tsx`
- Create: `src/web/src/features/course/__tests__/CoursePicker.test.tsx`

- [ ] **Step 1: Write the test for CoursePicker**

```tsx
// src/web/src/features/course/__tests__/CoursePicker.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { type ReactNode } from 'react';
import CoursePicker from '../pages/CoursePicker';

const mockNavigate = vi.fn();
vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return { ...actual, useNavigate: () => mockNavigate };
});

vi.mock('@/features/auth', () => ({
  useAuth: vi.fn(() => ({
    user: { role: 'Operator', organization: { name: 'Test Org' } },
    organizations: [],
  })),
}));

vi.mock('@/hooks/use-features', () => ({
  useFeature: vi.fn(() => true),
}));

vi.mock('../hooks/useCourses', () => ({
  useCourses: vi.fn(),
}));

vi.mock('../context/OrgContext', () => ({
  useOrgContext: vi.fn(() => ({
    org: null,
    selectOrg: vi.fn(),
    clearOrg: vi.fn(),
  })),
}));

import { useCourses } from '../hooks/useCourses';
import { useAuth } from '@/features/auth';

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  const router = createMemoryRouter(
    [
      { path: '/course', element: <CoursePicker /> },
      { path: '/course/:courseId/*', element: <div>Course Feature</div> },
    ],
    { initialEntries: ['/course'] },
  );

  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe('CoursePicker', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows course cards when multiple courses', () => {
    vi.mocked(useCourses).mockReturnValue({
      data: [
        { id: 'c1', name: 'Pine Valley', city: 'Clementon', state: 'NJ', timeZoneId: 'America/New_York' },
        { id: 'c2', name: 'Augusta', city: 'Augusta', state: 'GA', timeZoneId: 'America/New_York' },
      ],
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useCourses>);

    renderPage();

    expect(screen.getByText('Pine Valley')).toBeInTheDocument();
    expect(screen.getByText('Augusta')).toBeInTheDocument();
  });

  it('auto-navigates when single course', async () => {
    vi.mocked(useCourses).mockReturnValue({
      data: [
        { id: 'c1', name: 'Pine Valley', timeZoneId: 'America/New_York' },
      ],
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useCourses>);

    renderPage();

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/course/c1', expect.anything());
    });
  });

  it('shows org picker for admins', () => {
    vi.mocked(useAuth as ReturnType<typeof vi.fn>).mockReturnValue({
      user: { role: 'Admin' },
      organizations: [
        { id: 'org1', name: 'Org One' },
        { id: 'org2', name: 'Org Two' },
      ],
    });
    vi.mocked(useCourses).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useCourses>);

    renderPage();

    expect(screen.getByText('Select an Organization')).toBeInTheDocument();
    expect(screen.getByText('Org One')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm --dir src/web vitest run src/web/src/features/course/__tests__/CoursePicker.test.tsx`
Expected: FAIL — module `../pages/CoursePicker` not found

- [ ] **Step 3: Create CoursePicker page**

```tsx
// src/web/src/features/course/pages/CoursePicker.tsx
import { useEffect, useRef } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/features/auth';
import { useFeature } from '@/hooks/use-features';
import { useCourses } from '../hooks/useCourses';
import { useOrgContext } from '../context/OrgContext';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { PageTopbar } from '@/components/layout/PageTopbar';
import type { Course } from '@/types/course';

function formatLocation(course: Course): string {
  const parts = [course.city, course.state].filter(Boolean);
  return parts.length > 0 ? parts.join(', ') : 'Location not set';
}

function OrgPicker() {
  const { organizations } = useAuth();
  const { selectOrg } = useOrgContext();

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Select an Organization</h1>}
      />

      <div className="p-6">
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
      </div>
    </>
  );
}

function CourseList() {
  const navigate = useNavigate();
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
        navigate(`/course/${course.id}`, { replace: true });
      }
    }
  }, [coursesQuery.isLoading, coursesQuery.data, navigate]);

  function handleSelectCourse(course: Course) {
    navigate(`/course/${course.id}`, { replace: true });
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Select a Course</h1>}
      />

      <div className="p-6">
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

          {coursesQuery.data && coursesQuery.data.length > 1 && (
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
                  </CardHeader>
                </Card>
              ))}
            </div>
          )}
        </div>
      </div>
    </>
  );
}

export default function CoursePicker() {
  const { user } = useAuth();
  const { org } = useOrgContext();
  const isAdmin = user?.role === 'Admin';

  if (isAdmin && !org) {
    return <OrgPicker />;
  }

  return <CourseList />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm --dir src/web vitest run src/web/src/features/course/__tests__/CoursePicker.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/course/pages/CoursePicker.tsx src/web/src/features/course/__tests__/CoursePicker.test.tsx
git commit -m "feat: add CoursePicker page with admin org gate"
```

---

## Task 7: Wire Up CourseFeature with Feature Flag, Providers, and Course Picker

**Files:**
- Modify: `src/web/src/features/course/index.tsx`
- Modify: `src/web/src/features/course/pos/layouts/PosLayout.tsx`

This is the central wiring task. The course feature entry point needs to:
1. Wrap everything in `OrgProvider`
2. Handle the `/course` (no courseId) route → CoursePicker
3. Handle `/course/:courseId/*` → CourseProvider wrapping the existing routes
4. Use `full-operator-app` feature flag to control which routes are available
5. Render `minimal` shell variant (no sidebar) when the flag is off

- [ ] **Step 1: Update PosLayout to accept a variant prop**

In `src/web/src/features/course/pos/layouts/PosLayout.tsx`, update the component to accept an optional `variant` prop. When `variant="minimal"`, skip the nav config:

```tsx
// src/web/src/features/course/pos/layouts/PosLayout.tsx
import { Outlet } from 'react-router';
import { useCourseId } from '../../hooks/useCourseId';
import { AppShell, type NavConfig } from '@/components/layout/AppShell';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/features/auth';

function PosBrand() {
  const { user } = useAuth();
  return (
    <>
      <h1
        className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
        title={user?.organization?.name ?? 'Teeforce'}
      >
        {user?.organization?.name ?? 'Teeforce'}
      </h1>
      <Badge variant="default" className="text-[10px] px-1.5 py-0">
        POS
      </Badge>
    </>
  );
}

interface PosLayoutProps {
  variant?: 'full' | 'minimal';
}

export default function PosLayout({ variant = 'full' }: PosLayoutProps) {
  const courseId = useCourseId();

  const navConfig: NavConfig = {
    sections: [
      {
        label: 'Operations',
        items: [
          { to: `/course/${courseId}/pos/tee-sheet`, label: 'Tee Sheet' },
          { to: `/course/${courseId}/pos/waitlist`, label: 'Waitlist' },
        ],
      },
    ],
  };

  return (
    <AppShell
      variant={variant}
      navConfig={variant === 'full' ? navConfig : undefined}
      brand={<PosBrand />}
    >
      <Outlet />
    </AppShell>
  );
}
```

- [ ] **Step 2: Rewrite CourseFeature index**

Replace the entire contents of `src/web/src/features/course/index.tsx`:

```tsx
// src/web/src/features/course/index.tsx
import { Routes, Route, Navigate } from 'react-router';
import { ThemeProvider } from '@/components/ThemeProvider';
import { OrgProvider } from './context/OrgContext';
import { CourseProvider } from './context/CourseProvider';
import { useFeature } from '@/hooks/use-features';
import { useCourseId } from './hooks/useCourseId';
import ManagementLayout from './manage/layouts/ManagementLayout';
import PosLayout from './pos/layouts/PosLayout';
import Dashboard from './manage/pages/Dashboard';
import Schedule from './manage/pages/Schedule';
import ScheduleDay from './manage/pages/ScheduleDay';
import Settings from './manage/pages/Settings';
import TeeSheet from './pos/pages/TeeSheet';
import WalkUpWaitlist from './pos/pages/WalkUpWaitlist';
import CoursePicker from './pages/CoursePicker';

function CourseRoutes() {
  const courseId = useCourseId();
  const fullApp = useFeature('full-operator-app', courseId);

  if (!fullApp) {
    return (
      <Routes>
        <Route path="pos" element={<PosLayout variant="minimal" />}>
          <Route path="waitlist" element={<WalkUpWaitlist />} />
          <Route path="*" element={<Navigate to="waitlist" replace />} />
        </Route>
        <Route path="*" element={<Navigate to="pos/waitlist" replace />} />
      </Routes>
    );
  }

  return (
    <Routes>
      <Route path="manage" element={<ManagementLayout />}>
        <Route index element={<Dashboard />} />
        <Route path="schedule" element={<Schedule />} />
        <Route path="schedule/:date" element={<ScheduleDay />} />
        <Route path="settings" element={<Settings />} />
      </Route>
      <Route path="pos" element={<PosLayout />}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="waitlist" element={<WalkUpWaitlist />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
      <Route index element={<Navigate to="manage" replace />} />
      <Route path="*" element={<Navigate to="manage" replace />} />
    </Routes>
  );
}

function CourseWithProvider() {
  return (
    <CourseProvider>
      <CourseRoutes />
    </CourseProvider>
  );
}

export default function CourseFeature() {
  return (
    <ThemeProvider>
      <OrgProvider>
        <Routes>
          <Route index element={<CoursePicker />} />
          <Route path=":courseId/*" element={<CourseWithProvider />} />
        </Routes>
      </OrgProvider>
    </ThemeProvider>
  );
}
```

- [ ] **Step 3: Verify lint passes**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/course/index.tsx src/web/src/features/course/pos/layouts/PosLayout.tsx
git commit -m "feat: wire CourseFeature with feature flag, providers, and course picker"
```

---

## Task 8: Update Router — Remove Operator, Wire Course

**Files:**
- Modify: `src/web/src/app/router.tsx`

- [ ] **Step 1: Update the router**

In `src/web/src/app/router.tsx`:

Remove the `OperatorFeature` lazy import:
```tsx
const OperatorFeature = lazy(() => import('@/features/operator'));
```

Change `RoleRedirect` to navigate to `/course` instead of `/operator`:
```tsx
return <Navigate to="/course" replace />;
```

Remove the `/operator/*` route block entirely (lines 72–79):
```tsx
{
  path: 'operator/*',
  element: (
    <AuthGuard>
      <LazyFeature><OperatorFeature /></LazyFeature>
    </AuthGuard>
  ),
},
```

Replace the `/course` redirect route (lines 81–86) with the combined course route. Remove the separate `/course` and `/course/:courseId/*` routes and replace with a single route:
```tsx
{
  path: 'course/*',
  element: (
    <AuthGuard>
      <LazyFeature><CourseFeature /></LazyFeature>
    </AuthGuard>
  ),
},
```

Also add a redirect from `/operator` to `/course` for bookmarked URLs:
```tsx
{
  path: 'operator/*',
  element: <Navigate to="/course" replace />,
},
```

The final protected routes section should look like:
```tsx
children: [
  {
    index: true,
    element: (
      <AuthGuard>
        <RoleRedirect />
      </AuthGuard>
    ),
  },
  {
    path: 'admin/*',
    element: (
      <AuthGuard>
        <PermissionGuard permission="users:manage" fallback="/course">
          <LazyFeature><AdminFeature /></LazyFeature>
        </PermissionGuard>
      </AuthGuard>
    ),
  },
  {
    path: 'course/*',
    element: (
      <AuthGuard>
        <LazyFeature><CourseFeature /></LazyFeature>
      </AuthGuard>
    ),
  },
  {
    path: 'operator/*',
    element: <Navigate to="/course" replace />,
  },
],
```

Note: The `PermissionGuard` fallback also changed from `/operator` to `/course`.

- [ ] **Step 2: Verify lint passes**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add src/web/src/app/router.tsx
git commit -m "feat: update router to use /course as operator entry point"
```

---

## Task 9: Delete the Operator Feature

**Files:**
- Delete: `src/web/src/features/operator/` (entire directory, 46 files)

- [ ] **Step 1: Verify no remaining imports from operator**

Run: `grep -r "features/operator" src/web/src/ --include="*.ts" --include="*.tsx" -l`
Expected: No files outside of `src/web/src/features/operator/` should be listed. If `router.tsx` still references it, fix Task 8 first.

- [ ] **Step 2: Delete the operator feature directory**

```bash
rm -rf src/web/src/features/operator
```

- [ ] **Step 3: Verify the app compiles**

Run: `pnpm --dir src/web build`
Expected: Build succeeds with no errors

- [ ] **Step 4: Run all frontend tests**

Run: `pnpm --dir src/web test`
Expected: All tests pass. The 10 deleted operator tests no longer exist.

- [ ] **Step 5: Run lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: delete legacy operator feature

The course feature now handles all operator functionality:
- Course picker at /course (with admin org gate)
- Feature flag routing (full vs minimal shell)
- CourseProvider with route-driven timezone
- Dirty form detection on Settings
- /operator/* redirects to /course"
```

---

## Task 10: Smoke Test

- [ ] **Step 1: Start the dev server**

Run: `make dev`
Expected: API on :5221, Web on :3000

- [ ] **Step 2: Verify operator redirect**

Navigate to `http://localhost:3000/operator`
Expected: Redirects to `/course`

- [ ] **Step 3: Verify course picker → course selection → tee sheet**

1. At `/course`, verify course cards appear
2. Click a course → navigates to `/course/:courseId/manage` (if full flag) or `/course/:courseId/pos/waitlist` (if minimal)
3. Navigate to Tee Sheet and verify the date is correct (uses course timezone)

- [ ] **Step 4: Verify dirty form detection on Settings**

1. Navigate to `/course/:courseId/manage/settings`
2. Change a form field
3. Try navigating away → should see browser confirmation dialog
4. Cancel → stays on page
5. Save → "unsaved changes" banner disappears

- [ ] **Step 5: Stop dev server**

Run: `make down`
