# Implementation Plan: Issue #151 — Platform Admin Tenant + Course View

## Overview

Enhance the existing TenantList page with dashboard metrics and clickable navigation, expand the `CourseInfo` DTO to include location fields, create a new TenantDetail page, and add a route for `/admin/tenants/:id`. No new database entities, no migrations, no new backend endpoints.

---

## 1. Backend Changes

### 1.1 Expand `CourseInfo` Record

**File:** `src/api/Endpoints/TenantEndpoints.cs` (line 155)

**Current:**
```csharp
public record CourseInfo(Guid Id, string Name);
```

**Change to:**
```csharp
public record CourseInfo(Guid Id, string Name, string? City, string? State);
```

**Rationale:** AC #4 requires "each course's name and location." The `Course` entity already has `City` and `State` fields (see `src/api/Models/Course.cs` lines 9-10). We add them to the DTO so the frontend can display location without an extra round-trip.

### 1.2 Update `GetTenantById` Projection

**File:** `src/api/Endpoints/TenantEndpoints.cs` (line 104)

**Current:**
```csharp
tenant.Courses.Select(c => new CourseInfo(c.Id, c.Name)).ToList(),
```

**Change to:**
```csharp
tenant.Courses.Select(c => new CourseInfo(c.Id, c.Name, c.City, c.State)).ToList(),
```

This passes the `City` and `State` from the loaded `Course` navigation property into the response.

### 1.3 Query Filter Verification

**Risk identified in planning:** The global query filter on `Course` (line 37 of `ApplicationDbContext.cs`) is:
```csharp
c => _currentUser == null || _currentUser.TenantId == null || c.TenantId == _currentUser.TenantId
```

The `GetTenantById` endpoint (line 89) uses `db.Tenants.Include(t => t.Courses)`. When no `X-Tenant-Id` header is sent (platform admin requests), `_currentUser.TenantId` is `null`, so the filter passes and **all courses for the tenant are included**. This is correct. However, there is a subtlety: in the `TestWebApplicationFactory`, `ICurrentUser` may not be registered at all, which means `_currentUser` is `null` — the filter also passes. So the existing test infrastructure already handles this correctly.

**Action:** No code change needed. Add a comment in the plan confirming this was verified so future developers do not re-investigate.

### 1.4 Backend Integration Test Updates

**File:** `tests/api/TenantEndpointsTests.cs`

Update the `TenantDetailResponse` and `CourseInfo` records at the bottom of the test file to match the expanded DTO:

**Current (line 253):**
```csharp
private record CourseInfo(Guid Id, string Name);
```

**Change to:**
```csharp
private record CourseInfo(Guid Id, string Name, string? City, string? State);
```

**Add a new test** to verify courses include location data:

```csharp
[Fact]
public async Task GetTenantById_WithCourses_ReturnsCourseLocationData()
{
    // Create tenant
    var createTenantResponse = await _client.PostAsJsonAsync("/tenants", new
    {
        OrganizationName = "Location Test Tenant " + Guid.NewGuid(),
        ContactName = "Test Contact",
        ContactEmail = "test@location.com",
        ContactPhone = "555-0000"
    });
    var tenant = await createTenantResponse.Content.ReadFromJsonAsync<TenantResponse>();

    // Create course with location data
    await _client.PostAsJsonAsync("/courses", new
    {
        Name = "Location Course",
        TenantId = tenant!.Id,
        City = "Scottsdale",
        State = "AZ"
    });

    // Fetch tenant detail
    var response = await _client.GetAsync($"/tenants/{tenant.Id}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var detail = await response.Content.ReadFromJsonAsync<TenantDetailResponse>();
    Assert.NotNull(detail);
    Assert.Single(detail!.Courses);
    Assert.Equal("Location Course", detail.Courses[0].Name);
    Assert.Equal("Scottsdale", detail.Courses[0].City);
    Assert.Equal("AZ", detail.Courses[0].State);
}
```

---

## 2. Frontend Type Changes

### 2.1 Expand `TenantDetail.courses` Type

**File:** `src/web/src/types/tenant.ts`

**Current:**
```typescript
export interface TenantDetail extends Tenant {
  courses: Array<{
    id: string;
    name: string;
  }>;
}
```

**Change to:**
```typescript
export interface TenantDetail extends Tenant {
  courses: Array<{
    id: string;
    name: string;
    city: string | null;
    state: string | null;
  }>;
}
```

---

## 3. Frontend Hook Changes

### 3.1 Add `useTenant(id)` to Admin Hooks

**File:** `src/web/src/features/admin/hooks/useTenants.ts`

The `useTenant(id)` hook already exists in this file (lines 13-19). No changes needed to the hook logic. It fetches `GET /tenants/:id` and returns `TenantDetail`.

No modifications required.

---

## 4. Frontend Route Changes

### 4.1 Add TenantDetail Route

**File:** `src/web/src/features/admin/index.tsx`

**Current:**
```tsx
import TenantList from './pages/TenantList';
import TenantCreate from './pages/TenantCreate';
// ...
<Route path="tenants" element={<TenantList />} />
<Route path="tenants/new" element={<TenantCreate />} />
```

**Add import:**
```tsx
import TenantDetail from './pages/TenantDetail';
```

**Add route (between `tenants` and `tenants/new` to avoid matching conflicts — but actually route order does not matter with React Router v7 since it uses specificity, so just add it alongside the others):**
```tsx
<Route path="tenants/:id" element={<TenantDetail />} />
```

**Important:** The `tenants/new` route must remain. React Router v7 handles specificity correctly, so `/admin/tenants/new` will match the `tenants/new` route and `/admin/tenants/abc-123` will match `tenants/:id`. No ordering concern.

**Full updated file:**
```tsx
import { Routes, Route, Navigate } from 'react-router';
import AdminLayout from '@/components/layout/AdminLayout';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import TenantList from './pages/TenantList';
import TenantCreate from './pages/TenantCreate';
import TenantDetail from './pages/TenantDetail';

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="tenants" element={<TenantList />} />
        <Route path="tenants/new" element={<TenantCreate />} />
        <Route path="tenants/:id" element={<TenantDetail />} />
        <Route path="*" element={<Navigate to="courses" replace />} />
      </Route>
    </Routes>
  );
}
```

---

## 5. TenantList Page Enhancements

### 5.1 Modify TenantList Page

**File:** `src/web/src/features/admin/pages/TenantList.tsx`

**Changes required:**

#### 5.1.1 Add Imports

Add imports for:
- `{ useNavigate }` from `react-router`
- `{ Card, CardContent, CardHeader, CardTitle }` from `@/components/ui/card`
- `{ Badge }` from `@/components/ui/badge`
- `{ Skeleton }` from `@/components/ui/skeleton`

#### 5.1.2 Loading State — Replace Text with Skeleton Cards + Table

Replace the current loading `<p>` tag with skeleton placeholders:
- 3 skeleton cards in a grid row (matching the metric cards layout)
- Skeleton table rows below

Structure:
```tsx
<div className="p-6 space-y-6">
  {/* Skeleton metric cards */}
  <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
    {[1, 2, 3].map((i) => (
      <Card key={i}>
        <CardHeader>
          <Skeleton className="h-4 w-24" />
        </CardHeader>
        <CardContent>
          <Skeleton className="h-8 w-16" />
        </CardContent>
      </Card>
    ))}
  </div>
  {/* Skeleton table rows */}
  <div className="space-y-3">
    {[1, 2, 3].map((i) => (
      <Skeleton key={i} className="h-12 w-full" />
    ))}
  </div>
</div>
```

#### 5.1.3 Error State — Add "Try Again" Capability

Replace the current error `<p>` tag with a more structured error state:
```tsx
<div className="p-6 space-y-4">
  <p className="text-destructive">
    {error instanceof Error ? error.message : 'Failed to load tenants'}
  </p>
  <Button variant="outline" onClick={() => refetch()}>
    Try again
  </Button>
</div>
```

This requires extracting `refetch` from the `useTenants()` return value:
```tsx
const { data: tenants, isLoading, error, refetch } = useTenants();
```

#### 5.1.4 Dashboard Metric Cards

Add 3 metric cards above the table, after the header row. Compute metrics client-side from the existing `tenants` array:

```tsx
const totalTenants = tenants.length;
const totalCourses = tenants.reduce((sum, t) => sum + (t.courseCount ?? 0), 0);
const tenantsWithNoCourses = tenants.filter((t) => (t.courseCount ?? 0) === 0).length;
```

Render as a 3-column grid (stacks on mobile):
```tsx
<div className="grid grid-cols-1 md:grid-cols-3 gap-4">
  <Card aria-label="Total tenants">
    <CardHeader>
      <CardTitle className="text-sm font-medium text-muted-foreground">Total Tenants</CardTitle>
    </CardHeader>
    <CardContent>
      <div className="text-3xl font-bold">{totalTenants}</div>
    </CardContent>
  </Card>
  <Card aria-label="Total courses">
    <CardHeader>
      <CardTitle className="text-sm font-medium text-muted-foreground">Total Courses</CardTitle>
    </CardHeader>
    <CardContent>
      <div className="text-3xl font-bold">{totalCourses}</div>
    </CardContent>
  </Card>
  <Card aria-label="Tenants without courses">
    <CardHeader>
      <CardTitle className="text-sm font-medium text-muted-foreground">Tenants Without Courses</CardTitle>
    </CardHeader>
    <CardContent>
      <div className="text-3xl font-bold">{tenantsWithNoCourses}</div>
    </CardContent>
  </Card>
</div>
```

#### 5.1.5 Clickable Tenant Rows

Make the Organization Name cell a `Link` to `/admin/tenants/${tenant.id}`:

**Current (line 64):**
```tsx
<TableCell className="font-semibold">{tenant.organizationName}</TableCell>
```

**Change to:**
```tsx
<TableCell>
  <Link
    to={`/admin/tenants/${tenant.id}`}
    className="font-semibold text-primary hover:underline"
  >
    {tenant.organizationName}
  </Link>
</TableCell>
```

Also make the entire row clickable via `useNavigate` and `onClick`/cursor styling:

```tsx
<TableRow
  key={tenant.id}
  className="cursor-pointer hover:bg-muted/50"
  role="link"
  onClick={() => navigate(`/admin/tenants/${tenant.id}`)}
>
```

This requires `const navigate = useNavigate();` at the top of the component.

#### 5.1.6 Zero-Course Badge

**Current (lines 73-79):**
```tsx
<TableCell>
  {tenant.courseCount !== undefined ? (
    <span className={tenant.courseCount === 0 ? 'text-muted-foreground' : ''}>
      {tenant.courseCount === 0 ? 'None' : tenant.courseCount}
    </span>
  ) : (
    <span className="text-muted-foreground">—</span>
  )}
</TableCell>
```

**Change to:**
```tsx
<TableCell>
  {tenant.courseCount !== undefined ? (
    tenant.courseCount === 0 ? (
      <Badge variant="secondary">No courses</Badge>
    ) : (
      <span>{tenant.courseCount}</span>
    )
  ) : (
    <span className="text-muted-foreground">—</span>
  )}
</TableCell>
```

#### 5.1.7 Empty State Enhancement

**Current (lines 47-48):**
```tsx
<p className="text-muted-foreground">No tenants registered yet.</p>
```

**Change to:**
```tsx
<div role="status" className="text-center py-12">
  <p className="text-muted-foreground text-lg">No tenants registered yet.</p>
  <p className="text-muted-foreground text-sm mt-2">
    Register your first tenant to get started.
  </p>
</div>
```

#### 5.1.8 Contact Info Column Hidden on Mobile

Add `className="hidden md:table-cell"` to the Contact Info `<TableHead>` and its corresponding `<TableCell>`.

**Current:**
```tsx
<TableHead>Contact Info</TableHead>
```

**Change to:**
```tsx
<TableHead className="hidden md:table-cell">Contact Info</TableHead>
```

And the corresponding cell (around line 66):
```tsx
<TableCell className="hidden md:table-cell">
```

---

## 6. TenantDetail Page (New)

### 6.1 Create TenantDetail Page

**File to create:** `src/web/src/features/admin/pages/TenantDetail.tsx`

This is a new page component. It renders:
1. Back navigation link
2. Tenant org name as h1
3. Contact info in a 2-column grid
4. Registration date
5. Courses table (or empty state)
6. Not-found state when tenant ID is invalid

**Component structure:**

```tsx
import { useParams, Link } from 'react-router';
import { useTenant } from '../hooks/useTenants';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

export default function TenantDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: tenant, isLoading, error } = useTenant(id!);
```

**Loading state:**
```tsx
if (isLoading) {
  return (
    <div className="p-6 space-y-6">
      <Skeleton className="h-4 w-32" />
      <Skeleton className="h-8 w-64" />
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <Skeleton className="h-16 w-full" />
        <Skeleton className="h-16 w-full" />
      </div>
      <Skeleton className="h-48 w-full" />
    </div>
  );
}
```

**Not-found state (404):**

The `api-client.ts` throws an error with a `status` property (line 34-35). Check for 404:

```tsx
if (error) {
  const status = (error as Error & { status?: number }).status;
  if (status === 404) {
    return (
      <div className="p-6 space-y-4">
        <p className="text-muted-foreground text-lg">Tenant not found</p>
        <p className="text-muted-foreground text-sm">
          The tenant you are looking for does not exist or has been removed.
        </p>
        <Button variant="outline" asChild>
          <Link to="/admin/tenants">Back to Tenants</Link>
        </Button>
      </div>
    );
  }
  return (
    <div className="p-6 space-y-4">
      <p className="text-destructive">
        {error instanceof Error ? error.message : 'Failed to load tenant'}
      </p>
      <Button variant="outline" asChild>
        <Link to="/admin/tenants">Back to Tenants</Link>
      </Button>
    </div>
  );
}
```

**Success state:**

```tsx
return (
  <div className="p-6 space-y-6">
    {/* Back navigation */}
    <Button variant="ghost" size="sm" asChild>
      <Link to="/admin/tenants">← Back to Tenants</Link>
    </Button>

    {/* Tenant header */}
    <div>
      <h1 className="text-2xl font-bold">{tenant.organizationName}</h1>
      <p className="text-sm text-muted-foreground">
        Registered {new Date(tenant.createdAt).toLocaleDateString()}
      </p>
    </div>

    {/* Contact info grid */}
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      <div>
        <p className="text-sm font-medium text-muted-foreground">Contact Name</p>
        <p>{tenant.contactName}</p>
      </div>
      <div>
        <p className="text-sm font-medium text-muted-foreground">Contact Email</p>
        <p>{tenant.contactEmail}</p>
      </div>
      <div>
        <p className="text-sm font-medium text-muted-foreground">Contact Phone</p>
        <p>{tenant.contactPhone}</p>
      </div>
    </div>

    {/* Courses section */}
    <div>
      <h2 className="text-lg font-semibold mb-4">
        Courses ({tenant.courses.length})
      </h2>

      {tenant.courses.length === 0 ? (
        <div role="status" className="text-center py-8 border rounded-md">
          <p className="text-muted-foreground">
            No courses assigned to this tenant yet.
          </p>
        </div>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Location</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tenant.courses.map((course) => (
                <TableRow key={course.id}>
                  <TableCell className="font-semibold">{course.name}</TableCell>
                  <TableCell>
                    {course.city || course.state ? (
                      <span>
                        {course.city}
                        {course.city && course.state ? ', ' : ''}
                        {course.state}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  </div>
);
```

---

## 7. Test Strategy

### 7.1 Backend Integration Test

**File:** `tests/api/TenantEndpointsTests.cs`

**Changes:**
1. Update `CourseInfo` record to include `City` and `State` fields (as described in section 1.4)
2. Add `GetTenantById_WithCourses_ReturnsCourseLocationData` test (as described in section 1.4)

### 7.2 Frontend Tests: TenantList

**File to create:** `src/web/src/features/admin/__tests__/TenantList.test.tsx`

Follow the same pattern as `CourseList.test.tsx`:

```tsx
vi.mock('../hooks/useTenants');
import { useTenants } from '../hooks/useTenants';
const mockUseTenants = vi.mocked(useTenants);
```

**Test cases:**

1. **Shows loading state with skeletons**
   - Mock `useTenants` with `isLoading: true`
   - Assert skeleton elements are rendered (check for `data-slot="skeleton"` or the skeleton class)

2. **Shows error state with try again button**
   - Mock `useTenants` with `error: new Error('Network error')`
   - Assert error message and "Try again" button are present

3. **Shows empty state when no tenants exist**
   - Mock `useTenants` with `data: []`
   - Assert "No tenants registered yet." text

4. **Renders metric cards with correct counts**
   - Mock `useTenants` with sample data: 3 tenants, varying course counts (e.g., 2, 0, 5)
   - Assert:
     - "3" appears in Total Tenants card
     - "7" appears in Total Courses card
     - "1" appears in Tenants Without Courses card

5. **Renders tenant data in table**
   - Mock with tenant data including `courseCount`
   - Assert org name, contact name, and date are rendered

6. **Shows "No courses" badge for zero-course tenants**
   - Mock with a tenant that has `courseCount: 0`
   - Assert `Badge` with text "No courses" is rendered

7. **Organization name links to tenant detail**
   - Mock with tenant data
   - Assert link with `href` pointing to `/admin/tenants/{id}` exists

8. **Clickable row navigates to tenant detail**
   - Mock `useNavigate` from `react-router`
   - Mock with tenant data
   - Click the table row
   - Assert `navigate` was called with `/admin/tenants/{id}`

9. **Shows register tenant link**
   - Assert link to `/admin/tenants/new` exists

### 7.3 Frontend Tests: TenantDetail

**File to create:** `src/web/src/features/admin/__tests__/TenantDetail.test.tsx`

**Mock setup:**
```tsx
vi.mock('../hooks/useTenants');
import { useTenant } from '../hooks/useTenants';
const mockUseTenant = vi.mocked(useTenant);

vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useParams: () => ({ id: 'test-tenant-id' }),
  };
});
```

**Test cases:**

1. **Shows loading state with skeletons**
   - Mock `useTenant` with `isLoading: true`
   - Assert skeleton elements are rendered

2. **Shows not-found state for 404 error**
   - Mock `useTenant` with error that has `status: 404`
   - Assert "Tenant not found" text
   - Assert "Back to Tenants" link with href `/admin/tenants`

3. **Shows generic error state**
   - Mock `useTenant` with `error: new Error('Server error')`
   - Assert error message text
   - Assert "Back to Tenants" link

4. **Renders tenant details**
   - Mock with full tenant detail data
   - Assert org name heading
   - Assert contact name, email, phone are visible
   - Assert registration date is displayed

5. **Renders courses table with location**
   - Mock with tenant that has courses with city/state
   - Assert course name appears
   - Assert location (City, State format) appears

6. **Shows empty state when tenant has no courses**
   - Mock with tenant that has empty `courses` array
   - Assert "No courses assigned to this tenant yet." text

7. **Shows em dash for courses without location**
   - Mock with course where city and state are both null
   - Assert em dash character is rendered

8. **Back link navigates to tenant list**
   - Assert link with text containing "Back to Tenants" has href `/admin/tenants`

---

## 8. File Summary

### Files to Create
| File | Purpose |
|------|---------|
| `src/web/src/features/admin/pages/TenantDetail.tsx` | Tenant detail page with contact info and courses table |
| `src/web/src/features/admin/__tests__/TenantList.test.tsx` | Unit tests for enhanced TenantList |
| `src/web/src/features/admin/__tests__/TenantDetail.test.tsx` | Unit tests for TenantDetail |

### Files to Modify
| File | Changes |
|------|---------|
| `src/api/Endpoints/TenantEndpoints.cs` | Expand `CourseInfo` record with `City`, `State`; update projection |
| `tests/api/TenantEndpointsTests.cs` | Update `CourseInfo` record; add location data test |
| `src/web/src/types/tenant.ts` | Add `city`, `state` to courses array type |
| `src/web/src/features/admin/index.tsx` | Add `TenantDetail` import and route |
| `src/web/src/features/admin/pages/TenantList.tsx` | Add metrics cards, clickable rows, badge, skeleton loading, error retry |

### Files NOT Changed
| File | Reason |
|------|--------|
| `src/api/Models/Course.cs` | Already has `City` and `State` fields |
| `src/api/Models/Tenant.cs` | No changes needed |
| `src/api/Data/ApplicationDbContext.cs` | Query filter already works for admin (null tenant) |
| `src/web/src/features/admin/hooks/useTenants.ts` | `useTenant(id)` hook already exists |
| `src/web/src/lib/query-keys.ts` | `tenants.detail(id)` key already exists |
| `src/web/src/lib/api-client.ts` | Error status already attached |
| `src/web/src/hooks/useTenants.ts` | Shared hook, not used by admin pages (admin has its own) |
| Any migration files | No schema changes |

---

## 9. Implementation Order

The recommended implementation order minimizes broken states:

1. **Backend DTO + test** — Expand `CourseInfo`, update projection, update test record, add new test. Run `dotnet build shadowbrook.slnx` and `dotnet test` to verify.
2. **Frontend type** — Update `TenantDetail` interface in `types/tenant.ts`.
3. **TenantDetail page** — Create the new page component.
4. **TenantDetail route** — Add the route in `admin/index.tsx`.
5. **TenantDetail tests** — Create tests for the new page.
6. **TenantList enhancements** — Modify TenantList with metrics, clickable rows, badges, skeletons, error retry.
7. **TenantList tests** — Create tests for the enhanced TenantList.
8. **Final verification** — Run `pnpm --dir src/web lint` and `pnpm --dir src/web test`.

---

## 10. Acceptance Criteria Mapping

| AC | Implementation |
|----|---------------|
| 1. Tenant list shows course count, zero-course tenants distinguishable | TenantList: `Badge variant="secondary"` for zero-course; numeric for others |
| 2. Empty state when no tenants | TenantList: `role="status"` div with message |
| 3. Click tenant navigates to detail with org name, contact, reg date | TenantList: clickable rows + Link; TenantDetail: h1, contact grid, date |
| 4. Detail shows courses with name and location | TenantDetail: courses Table with Name, Location (City, State) columns |
| 5. Detail empty state for no courses | TenantDetail: `role="status"` div with "No courses assigned" |
| 6. Dashboard metrics | TenantList: 3 metric Cards computed from data |
| 7. Recently added identifiable by registration date | TenantList: "Registered" column with formatted date (existing) |
| 8. Tenant-not-found error with return navigation | TenantDetail: 404 detection + "Back to Tenants" link |

---

## 11. Risks and Edge Cases

1. **Shared `TestWebApplicationFactory` state:** Integration tests share an in-memory SQLite database across all test classes in the assembly. The new test creates its own tenant/course, so it will not conflict with other tests, but the `GetAllTenants_WithNoTenants_ReturnsEmptyArray` test is already fragile (it may see tenants from other test runs). This is a pre-existing issue — do not address in this PR.

2. **Duplicate `useTenants` hook:** There are two copies — `src/web/src/hooks/useTenants.ts` (shared) and `src/web/src/features/admin/hooks/useTenants.ts` (admin-specific). The admin pages import from the feature-local one, which also exports `useTenant(id)`. This is correct per the import rules (features use their own hooks). Do not consolidate them in this PR.

3. **Link vs useNavigate on table rows:** Using both a `<Link>` on the org name cell AND `onClick` + `useNavigate` on the `<TableRow>` means clicking the link will trigger both handlers. Prevent double-navigation by calling `e.stopPropagation()` on the `Link`'s click, or simply make only the row clickable (not the cell link independently). The recommended approach: make the row clickable via `onClick` and style the org name as a visual link (underline on hover) but keep it as a `Link` for accessibility (right-click "Open in new tab" support). Add `onClick={(e) => e.stopPropagation()}` to the `Link` to prevent the row handler from also firing.
