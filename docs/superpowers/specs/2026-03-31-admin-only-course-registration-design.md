# Admin-Only Course Registration

## Goal

Remove the operator's ability to register courses. Only platform admins can create courses. Operators select from courses already provisioned for their tenant.

## Motivation

Course registration is a platform administration concern, not an operator self-service action. Centralizing it under the admin role simplifies the operator experience and gives platform admins full control over course provisioning.

## Scope

Frontend only. No backend or admin-side changes.

## Changes

### 1. Remove operator register-course route

**File:** `src/web/src/features/operator/index.tsx`

- Remove the `/register-course` route from the operator route tree
- Remove the special-case in `CourseGate` that allows navigating to `/operator/register-course` without a selected course

### 2. Remove "Register Course" from operator sidebar

**File:** `src/web/src/components/layout/OperatorLayout.tsx`

- Remove the "Register Course" navigation link from the operator sidebar menu

### 3. Update CoursePortfolio empty state

**File:** `src/web/src/features/operator/pages/CoursePortfolio.tsx`

- Replace the "Get started by adding your first course" message and register button with: "No courses available. Contact your administrator to add a course."
- Remove the link/button to `/operator/register-course`

### 4. Delete operator CourseRegister page

**File:** `src/web/src/features/operator/pages/CourseRegister.tsx`

- Delete entirely. No longer referenced.

### 5. Delete useCourseRegister hook

**File:** `src/web/src/features/operator/hooks/useCourseRegister.ts`

- Delete entirely. Only used by the operator CourseRegister page.

### 6. Clean up E2E fixtures

**File:** `src/web/e2e/fixtures/operator-register-page.ts`

- Delete the operator register page object
- Remove any imports/usage in test files that reference it

## What stays unchanged

- **Backend:** `POST /courses` endpoint remains — admins use it via `CourseCreate.tsx`
- **Admin UI:** `/admin/courses/new` (CourseCreate) and `/admin/courses` (CourseList) are untouched
- **Operator flows:** Tenant selection, course selection, tee sheet, waitlist, settings all unchanged
