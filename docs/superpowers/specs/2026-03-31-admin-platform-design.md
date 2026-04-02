# Admin Platform Design

## Overview

Internal admin tooling for managing the Shadowbrook platform — organizations, courses, users, feature flags, and analytics. This is Aaron's operational tool, not a self-service experience.

## Role Model Simplification

Two roles replace the current three:

| Role | Scope | Access |
|------|-------|--------|
| **Admin** | Platform | All orgs, all courses, user management, feature flags, analytics |
| **Operator** | Organization | All courses within their org, tee sheet/POS |

Changes from current model:
- `AppUserRole.Owner` renamed to `AppUserRole.Operator`
- `AppUserRole.Staff` removed
- `CourseAssignment` entity deleted — Operators access all courses in their org
- `CourseAccessAuthorizationHandler` removed — course access checked via org membership
- `ICurrentUser.CourseIds` removed; `OrganizationId` is the sole scoping mechanism
- Permissions unchanged: Admin gets `app:access` + `users:manage`, Operator gets `app:access`

EF migration: rename `Owner` → `Operator` in role column, drop `CourseAssignments` table.

## Backend Changes

### Domain

- Remove `CourseAssignment` entity and `CourseAssignments` collection from `AppUser`
- Rename `Owner` → `Operator` in `AppUserRole` enum
- Remove `AddCourseAssignment` / `RemoveCourseAssignment` methods from `AppUser`

### Auth

- `AppUserEnrichmentMiddleware`: stop adding `course_id` claims, rename `Owner` references to `Operator`
- Remove `CourseAccessAuthorizationHandler` and `RequireCourseAccess` policy
- `CurrentUser`: remove `CourseIds` property
- Course access for Operators: verify `course.OrganizationId == currentUser.OrganizationId`

### API Endpoints

**Modified:**

| Endpoint | Change |
|----------|--------|
| `PUT /auth/users/{id}` | Expand to handle role and org assignment |
| `PUT /auth/users/{id}/courses` | Remove — no more course assignments |

**New — Organization management:**

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `PUT /organizations/{id}` | `users:manage` | Edit organization |

**New — Course management:**

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `PUT /courses/{courseId}` | `users:manage` | Edit course |

**New — Feature flag management:**

| Endpoint | Auth | Purpose |
|----------|------|---------|
| `PUT /organizations/{id}/features` | `users:manage` | Set org-level feature flags |
| `PUT /courses/{courseId}/features` | `users:manage` | Set course-level feature flags |

**New — Analytics (all require `users:manage`):**

| Endpoint | Purpose |
|----------|---------|
| `GET /admin/analytics/summary` | Platform totals — orgs, courses, users, bookings today |
| `GET /admin/analytics/fill-rates` | Tee time fill rates by course and date range |
| `GET /admin/analytics/bookings` | Booking counts over time |
| `GET /admin/analytics/popular-times` | Aggregated booking counts by time slot |
| `GET /admin/analytics/waitlist` | Waitlist stats — active entries, offers, conversions |

### Analytics Query Approach

Use `Database.SqlQuery<T>()` (EF Core 10) for all analytics queries:
- Raw SQL mapped to record DTOs — no entity tracking overhead
- Parameterized via interpolated strings (SQL injection safe)
- Tenant scoping via explicit `WHERE` clauses (EF query filters don't apply to raw SQL)
- No additional dependencies (no Dapper)

Organized under `Features/Analytics/` with read-model records.

Example:
```csharp
public record FillRateResult(DateOnly Date, int FilledSlots, int TotalSlots, decimal FillPercentage);

var stats = await db.Database.SqlQuery<FillRateResult>(
    $"""
    SELECT CAST(t.TeeTime AS DATE) as Date,
           COUNT(DISTINCT cs.GolferId) as FilledSlots,
           SUM(t.SlotsAvailable) as TotalSlots,
           CAST(COUNT(DISTINCT cs.GolferId) AS FLOAT) / NULLIF(SUM(t.SlotsAvailable), 0) * 100 as FillPercentage
    FROM TeeTimeOpenings t
    LEFT JOIN ClaimedSlots cs ON cs.TeeTimeOpeningId = t.Id
    WHERE t.CourseId = {courseId}
    GROUP BY CAST(t.TeeTime AS DATE)
    """).ToListAsync();
```

## Frontend Changes

### Layout

Existing `AdminLayout` with updated sidebar nav:
- Dashboard (analytics)
- Organizations
- Courses
- Users
- Feature Flags

Existing tenant pages replaced by organization pages.

### Routes

| Route | Component | Purpose |
|-------|-----------|---------|
| `/admin` | Dashboard | Analytics — summary cards, fill rate charts, booking trends, popular times, waitlist stats |
| `/admin/organizations` | OrgList | List all organizations |
| `/admin/organizations/new` | OrgCreate | Create organization |
| `/admin/organizations/:id` | OrgDetail | View/edit org, its courses and users |
| `/admin/courses` | CourseList | List all courses (filterable by org) |
| `/admin/courses/new` | CourseCreate | Create course under an org |
| `/admin/courses/:id` | CourseDetail | View/edit course |
| `/admin/users` | UserList | List all users with role, org, status |
| `/admin/users/new` | UserCreate | Create user — email, name, role, org |
| `/admin/users/:id` | UserDetail | Edit role, org, activate/deactivate |
| `/admin/feature-flags` | FeatureFlags | Grid of flags × orgs/courses with toggles |

### Charts

Recharts for analytics visualizations — lightweight, composable, React-native.

### Auth References

All frontend references to `Owner` role renamed to `Operator`. `Staff` references removed. No more course assignment UI.

## Tenant → Organization Transition

The existing `Tenant` entity is a legacy registration concept (name, contact info). `Organization` is the real tenant unit with auth integration, feature flags, and course ownership. This spec:
- Replaces admin tenant pages with organization pages
- Removes tenant API endpoints and frontend hooks (`useTenants`, `TenantList`, etc.)
- Does **not** drop the `Tenant` table or entity yet — that's a separate cleanup task once all references are migrated

## Migration Strategy

Single EF migration covering:
1. Rename `Owner` → `Operator` in `AppUser.Role` column
2. Drop `CourseAssignments` table
3. Remove related indexes

Pre-launch app, no production data to protect — clean rename in place.
