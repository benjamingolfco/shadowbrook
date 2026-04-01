# Admin-Only Course Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove operator course registration so only platform admins can create courses.

**Architecture:** Pure frontend removal — delete the operator CourseRegister page, its hook, and all references. Update empty states to direct operators to contact their admin. Update E2E tests to use the admin flow instead.

**Tech Stack:** React 19, TypeScript, React Router, TanStack Query, Vitest, Playwright

---

### Task 1: Update CoursePortfolio empty state and tests

**Files:**
- Modify: `src/web/src/features/operator/pages/CoursePortfolio.tsx:78-93`
- Modify: `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx:81-111`

- [ ] **Step 1: Update the failing tests first**

In `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx`, replace the two empty-state tests:

Replace the test at line 81 (`'shows CTA when no courses registered'`) with:

```typescript
  it('shows admin contact message when no courses registered', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(screen.getByText('No courses available')).toBeInTheDocument();
    expect(screen.getByText('Contact your administrator to add a course.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Register a Course' })).not.toBeInTheDocument();
  });
```

Delete the test at line 96 (`'navigates to register-course when Register a Course is clicked'`) entirely — it tests removed functionality.

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm --dir src/web test -- --reporter verbose src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx`
Expected: 1 failing test (`'shows admin contact message when no courses registered'`), and the deleted navigation test no longer runs.

- [ ] **Step 3: Update CoursePortfolio empty state**

In `src/web/src/features/operator/pages/CoursePortfolio.tsx`, replace the empty-state block (lines 78-93):

```tsx
  if (!coursesQuery.data || coursesQuery.data.length === 0) {
    return (
      <div className="flex h-full items-center justify-center">
        <div className="w-full max-w-3xl space-y-6 p-8 text-center">
          <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Select a Course</h1>
          <p className="text-base font-medium font-[family-name:var(--font-heading)]">No courses available</p>
          <p className="text-sm text-muted-foreground">
            Contact your administrator to add a course.
          </p>
        </div>
      </div>
    );
  }
```

Also remove the `useNavigate` import and `const navigate = useNavigate();` line — they are no longer used in this file.

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm --dir src/web test -- --reporter verbose src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx`
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/operator/pages/CoursePortfolio.tsx src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx
git commit -m "feat: replace operator course registration CTA with admin contact message"
```

---

### Task 2: Update CourseSwitcher empty state

**Files:**
- Modify: `src/web/src/features/operator/components/CourseSwitcher.tsx:117-126`

- [ ] **Step 1: Update the CourseSwitcher zero-courses block**

In `src/web/src/features/operator/components/CourseSwitcher.tsx`, replace the empty-state block (lines 117-126):

```tsx
  if (coursesQuery.data?.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">No courses available</p>
    );
  }
```

This removes the "Register Course" NavLink. Also remove the `NavLink` import from `react-router` at line 2 — it's no longer used. Change line 2 from:

```tsx
import { NavLink } from 'react-router';
```

to remove the import entirely (NavLink is not used elsewhere in this file).

- [ ] **Step 2: Run lint to verify**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/components/CourseSwitcher.tsx
git commit -m "feat: remove register course link from CourseSwitcher empty state"
```

---

### Task 3: Remove register-course from operator sidebar

**Files:**
- Modify: `src/web/src/components/layout/OperatorLayout.tsx:73-83`

- [ ] **Step 1: Remove the Register Course menu item**

In `src/web/src/components/layout/OperatorLayout.tsx`, delete the entire `SidebarMenuItem` block for "Register Course" (lines 74-83):

```tsx
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/register-course">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Register Course</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
```

- [ ] **Step 2: Run lint to verify**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/components/layout/OperatorLayout.tsx
git commit -m "feat: remove Register Course from operator sidebar navigation"
```

---

### Task 4: Remove register-course route and CourseGate special case

**Files:**
- Modify: `src/web/src/features/operator/index.tsx`

- [ ] **Step 1: Remove imports, route, and gate logic**

In `src/web/src/features/operator/index.tsx`:

1. Delete line 6: `import CourseRegister from './pages/CourseRegister';`

2. In the `CourseGate` function, remove the special-case `if` block for register-course (lines 17-26):

```tsx
    if (location.pathname === '/operator/register-course') {
      return (
        <Routes>
          <Route element={<OperatorLayout />}>
            <Route path="register-course" element={<CourseRegister />} />
          </Route>
        </Routes>
      );
    }
```

3. Also remove the `useLocation` import and `const location = useLocation();` — they are no longer needed since the only consumer was the removed `if` block.

4. Remove the register-course route from the course-selected routes block (line 42):

```tsx
        <Route path="register-course" element={<CourseRegister />} />
```

The final `CourseGate` function should look like:

```tsx
function CourseGate() {
  const { course } = useCourseContext();

  if (!course) {
    return (
      <Routes>
        <Route element={<OperatorLayout />}>
          <Route path="*" element={<CoursePortfolio />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<OperatorLayout />}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="waitlist" element={<WalkUpWaitlist />} />
        <Route path="settings" element={<TeeTimeSettings />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
    </Routes>
  );
}
```

- [ ] **Step 2: Run lint to verify**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/index.tsx
git commit -m "feat: remove register-course route and CourseGate special case"
```

---

### Task 5: Delete operator CourseRegister page and useCourseRegister hook

**Files:**
- Delete: `src/web/src/features/operator/pages/CourseRegister.tsx`
- Delete: `src/web/src/features/operator/hooks/useCourseRegister.ts`

- [ ] **Step 1: Delete the files**

```bash
rm src/web/src/features/operator/pages/CourseRegister.tsx
rm src/web/src/features/operator/hooks/useCourseRegister.ts
```

- [ ] **Step 2: Run lint and tests to verify nothing references them**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test`
Expected: No import errors, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add -u src/web/src/features/operator/pages/CourseRegister.tsx src/web/src/features/operator/hooks/useCourseRegister.ts
git commit -m "chore: delete operator CourseRegister page and useCourseRegister hook"
```

---

### Task 6: Update E2E walkup flow test to use admin registration

The E2E test `walkup-flow.spec.ts` currently uses the operator `OperatorRegisterPage` to register a course before testing the walkup flow. Now that operators can't register courses, this test needs to register via the admin UI instead.

**Files:**
- Modify: `src/web/e2e/tests/walkup/walkup-flow.spec.ts:1-21`
- Modify: `src/web/e2e/fixtures/operator-register-page.ts` (convert to admin registration)

- [ ] **Step 1: Convert OperatorRegisterPage to AdminRegisterPage**

Rename and rewrite `src/web/e2e/fixtures/operator-register-page.ts` to `src/web/e2e/fixtures/admin-register-page.ts`:

```typescript
import { type Page } from '@playwright/test';

export class AdminRegisterPage {
  constructor(private readonly page: Page) {}

  async goto() {
    await this.page.goto('/admin/courses/new');
  }

  async registerCourse(courseName: string, tenantName: string) {
    // Select tenant from shadcn Select dropdown
    await this.page.getByRole('combobox').click();
    await this.page.getByRole('option', { name: tenantName }).click();

    // Fill course name
    await this.page.getByLabel('Course Name *').fill(courseName);

    // Timezone is auto-filled from browser — leave it
    await this.page.getByRole('button', { name: 'Register Course' }).click();

    // After registration, the app navigates to /admin/courses (CourseList).
    // Wait for the list heading to confirm navigation completed.
    await this.page.getByRole('heading', { name: 'All Registered Courses' }).waitFor();
  }
}
```

- [ ] **Step 2: Update walkup-flow.spec.ts to use admin registration**

In `src/web/e2e/tests/walkup/walkup-flow.spec.ts`, update the imports and first test:

Replace lines 1-2:

```typescript
import { test, expect } from '../../fixtures/auth';
import { AdminRegisterPage } from '../../fixtures/admin-register-page';
```

Replace the first test (`'operator registers a new course'`, lines 14-21):

```typescript
  test('admin registers a new course', async ({ page, asAdmin }) => {
    await asAdmin();
    const register = new AdminRegisterPage(page);

    await register.goto();
    await register.registerCourse(courseName, TEST_TENANT_NAME);
  });
```

The `asAdmin` fixture is already defined in `src/web/e2e/fixtures/auth.ts:31-35`.

- [ ] **Step 3: Delete the old operator-register-page fixture**

```bash
rm src/web/e2e/fixtures/operator-register-page.ts
```

- [ ] **Step 4: Verify the admin course creation page has the expected locators**

Read `src/web/src/features/admin/pages/CourseCreate.tsx` to confirm the form labels match the locators used in `AdminRegisterPage` (tenant label, course name label, submit button text). Adjust the fixture if labels differ.

- [ ] **Step 5: Commit**

```bash
git add src/web/e2e/fixtures/admin-register-page.ts src/web/e2e/tests/walkup/walkup-flow.spec.ts
git add -u src/web/e2e/fixtures/operator-register-page.ts
git commit -m "test: update E2E walkup flow to register courses via admin UI"
```

---

### Task 7: Final verification

- [ ] **Step 1: Run full frontend lint**

Run: `pnpm --dir src/web lint`
Expected: No errors.

- [ ] **Step 2: Run full frontend test suite**

Run: `pnpm --dir src/web test`
Expected: All tests pass.

- [ ] **Step 3: Verify no stale references**

Search for any remaining references to the removed files:

```bash
grep -r "CourseRegister\|useCourseRegister\|useRegisterCourse\|register-course\|operator-register-page" src/web/src/ src/web/e2e/ --include="*.ts" --include="*.tsx"
```

Expected: No matches (other than potentially the admin-register-page fixture which is intentional).
