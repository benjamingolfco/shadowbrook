# Remove Legacy Operator Feature

**Issue:** #415  
**Date:** 2026-04-10

## Summary

The weekly tee sheet setup PR (#414) introduced `/course/:courseId/*` routes that duplicate the operator feature's pages and components. This spec covers deleting `features/operator/`, fixing two gaps in the course feature, adding a course picker at `/course`, and wiring up the feature flag for minimal shell support.

## Route Structure (After)

```
/course                         → Course picker (with admin org gate)
/course/:courseId/manage        → Dashboard (feature flag: full only)
/course/:courseId/manage/schedule     → Schedule (feature flag: full only)
/course/:courseId/manage/schedule/:date → Schedule day detail (feature flag: full only)
/course/:courseId/manage/settings     → Settings (feature flag: full only)
/course/:courseId/pos/tee-sheet → Tee Sheet (feature flag: full only)
/course/:courseId/pos/waitlist  → Walk-up Waitlist (always available)
```

`RoleRedirect` sends operators to `/course` instead of `/operator`.

## Feature Flag Behavior

The `full-operator-app` feature flag (per-course) controls what operators see:

| Flag State | Shell Variant | Available Routes | Default Landing |
|------------|--------------|------------------|-----------------|
| Enabled | `full` (sidebar) | All manage + pos routes | `/course/:courseId/manage` |
| Disabled | `minimal` (no sidebar) | `/course/:courseId/pos/waitlist` only | `/course/:courseId/pos/waitlist` |

The course picker page checks the flag after course selection to decide where to navigate. `CourseFeature` conditionally renders routes based on the flag.

## Course Picker Page (`/course`)

New page at the root of the course feature, rendered when no `courseId` is in the URL.

### Operator Flow
1. GET `/courses` loads the operator's courses (filtered by EF query filters)
2. If only one course, auto-select and navigate to `/course/:courseId`
3. Otherwise show course cards; click navigates to `/course/:courseId`

### Admin Flow
1. Admin sees org picker first (orgs from `useAuth().organizations`)
2. Select org → sets `X-Organization-Id` header via `setAdminOrgIdGetter`
3. Courses load for selected org
4. Course selection works same as operator flow

### OrgContext

Move `OrgContext` from `features/operator/context/` to `features/course/context/`. Same implementation — manages admin org impersonation via `setAdminOrgIdGetter` on the API client. Wrap the course feature entry point in `OrgProvider`.

### useCourses Hook

Move from `features/operator/hooks/` to `features/course/hooks/`. Same implementation — GET `/courses` with TanStack Query key `['courses']`.

## Gap Fix: Course Timezone

**Problem:** Course POS files use `getBrowserTimeZone()` instead of the course's `timeZoneId`. If the operator's browser timezone differs from the course timezone, dates are wrong.

**Fix:** Add a `useCourse(courseId)` hook that fetches course metadata (GET `/courses/:courseId` or similar). The existing GET `/courses` endpoint returns `timeZoneId` per course. Options:

1. **Use existing endpoint** GET `/courses/:courseId` — already exists, returns `timeZoneId`
2. **Reuse list endpoint** — filter client-side from cached `['courses']` query

Option 1 is cleaner. The endpoint already exists at `CourseEndpoints.GetCourseById`.

Expose `timeZoneId` via a `CourseProvider` that wraps all `/course/:courseId/*` routes and provides course metadata through context. This replaces the old `CourseContext` (which stored user-selected course) with a route-driven provider (which loads course data from the courseId param).

**Files to update** (replace `getBrowserTimeZone()` with course timezone from context):
- `features/course/pos/pages/TeeSheet.tsx`
- `features/course/pos/pages/WalkUpWaitlist.tsx`
- `features/course/pos/components/PostTeeTimeForm.tsx`
- `features/course/pos/components/AddTeeTimeOpeningDialog.tsx`

## Gap Fix: Dirty Form Detection

**Problem:** The Settings page in course/manage has no unsaved changes warning. The operator version tracked this via CourseContext.

**Fix:** Implement dirty form tracking as local state within the Settings page using React Hook Form's `formState.isDirty`. This is a single-page concern and doesn't need a context. Use `useBlocker` from React Router to warn on navigation when the form is dirty.

## What Gets Deleted

The entire `features/operator/` directory:

- **Pages:** TeeSheet, WalkUpWaitlist, TeeTimeSettings, CoursePortfolio, OrgPicker
- **Components:** All dialogs, grids, helpers, display components (duplicated in course feature)
- **Hooks:** useTeeSheet, useWaitlist, useWalkUpWaitlist, useTeeTimeSettings, useOperatorShellProps
- **Context:** CourseContext (replaced by route params + CourseProvider)
- **Tests:** All operator tests
- **Entry point:** index.tsx, navigation.tsx

## What Moves to Course Feature

| From (operator) | To (course) | Notes |
|-----------------|-------------|-------|
| `context/OrgContext.tsx` | `features/course/context/OrgContext.tsx` | Same implementation |
| `hooks/useCourses.ts` | `features/course/hooks/useCourses.ts` | Same implementation |

## Router Changes (`app/router.tsx`)

- Remove `/operator/*` route and `OperatorFeature` lazy import
- Remove `/course` → `/operator` redirect
- `RoleRedirect`: change `/operator` to `/course`
- `/course` (no courseId) renders the course picker page within the course feature

## New Files

| File | Purpose |
|------|---------|
| `features/course/context/OrgContext.tsx` | Admin org impersonation (moved from operator) |
| `features/course/hooks/useCourses.ts` | Course listing hook (moved from operator) |
| `features/course/hooks/useCourse.ts` | Single course metadata hook (uses existing GET `/courses/:courseId`) |
| `features/course/context/CourseProvider.tsx` | Provides course metadata (timeZoneId, name) to route subtree |
| `features/course/pages/CoursePicker.tsx` | Course selection page with admin org gate |

## What Stays Unchanged

- `features/admin/` — admin feature has its own course/org management, unaffected
- `features/auth/` — auth flow unchanged
- All public routes (`/golfer`, `/join`, `/book/walkup`, `/w`)
- Backend course/org endpoints (except possibly adding GET `/courses/:courseId`)
