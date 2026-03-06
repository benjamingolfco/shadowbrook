# Implementation Plan: Issue #185 — Walk-Up Waitlist Management

**Issue:** #185 (As a course operator, I can enable and manage the walk-up waitlist for today)
**Story Points:** 8
**Date:** 2026-03-05

---

## 1. Scope Clarification

Issue #185 is distinct from the original waitlist architecture (#180). While #180 focused on operator-initiated tee-time-specific waitlist requests (add a specific tee time to the waitlist), #185 introduces a **walk-up session model**: the operator opens a daily waitlist session, gets a short code golfers can use to join, monitors the queue, and closes it when done.

This story creates the `CourseWaitlist` entity from the architecture doc but extends it with walk-up session fields (`ShortCode`, `Status`, `OpenedAt`, `ClosedAt`). The `WaitlistRequest` entity (tee-time-specific offers) is NOT needed for this story — it belongs to #180.

**Key difference from the architecture doc:** The architecture doc envisions `CourseWaitlist` as a bare container created lazily. For #185, the operator explicitly opens/closes the waitlist session, so `CourseWaitlist` gains lifecycle fields. This is additive — it does not conflict with the architecture doc's design.

---

## 2. Data Model

### 2.1 New Entity: `CourseWaitlist`

```
CourseWaitlist
  Id              Guid, PK
  CourseId         Guid, FK -> Course, required
  Date            DateOnly, required
  ShortCode       string, required, max length 4
  Status          string, required ("Open" or "Closed")
  OpenedAt        DateTimeOffset, required
  ClosedAt        DateTimeOffset?, nullable
  CreatedAt       DateTimeOffset, required
  UpdatedAt       DateTimeOffset, required

  Unique constraint: (CourseId, Date) — one waitlist per course per day
  Index: (ShortCode, Date) — for golfer lookup by short code + today
  Index: (CourseId, Date) — covered by the unique constraint
```

**Status values:** `Open`, `Closed`

**ShortCode generation algorithm:**
1. Generate a random 4-digit string (0000-9999) using `Random.Shared.Next(0, 10000).ToString("D4")`
2. Check global uniqueness for today's date: `WHERE ShortCode = @code AND Date = @today`
3. If collision, regenerate (retry up to 10 times, then fail — with 10,000 possible codes and likely <100 courses active on any day, collision probability is negligible)
4. Short codes are globally unique per day (not just per-course) to prevent cross-course confusion when golfers type them in

**Why not per-course uniqueness?** If two courses had the same short code on the same day and a golfer typed it in, the system would need to disambiguate. Global uniqueness eliminates this entirely.

### 2.2 Navigation Property on Course

Add to `Course`:
```
Course (existing)
  + Waitlists    ICollection<CourseWaitlist>  (navigation, no new column)
```

No `WaitlistEnabled` column — per the issue, walk-up waitlist is always enabled for all courses. No feature flag needed.

### 2.3 EF Core Configuration

In `ApplicationDbContext.OnModelCreating`:

```
modelBuilder.Entity<CourseWaitlist>()
    .HasOne(w => w.Course)
    .WithMany(c => c.Waitlists)
    .HasForeignKey(w => w.CourseId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<CourseWaitlist>()
    .HasIndex(w => new { w.CourseId, w.Date })
    .IsUnique();

modelBuilder.Entity<CourseWaitlist>()
    .HasIndex(w => new { w.ShortCode, w.Date });

modelBuilder.Entity<CourseWaitlist>()
    .Property(w => w.ShortCode)
    .HasMaxLength(4);

modelBuilder.Entity<CourseWaitlist>()
    .Property(w => w.Status)
    .HasMaxLength(10);
```

### 2.4 Migration

Migration name: `AddCourseWaitlist`

Creates `CourseWaitlists` table with all fields, constraints, and indexes above.

---

## 3. Backend Implementation

### 3.1 Files to Create

#### `src/api/Models/CourseWaitlist.cs`

```
CourseWaitlist entity class with:
- Id (Guid)
- CourseId (Guid, required)
- Date (DateOnly, required)
- ShortCode (string, required)
- Status (string, required)
- OpenedAt (DateTimeOffset, required)
- ClosedAt (DateTimeOffset?, nullable)
- CreatedAt (DateTimeOffset)
- UpdatedAt (DateTimeOffset)
- Course navigation property (Course?)
```

#### `src/api/Endpoints/WalkUpWaitlistEndpoints.cs`

Extension method: `MapWalkUpWaitlistEndpoints(this WebApplication app)`

Route group: `/courses/{courseId:guid}/walkup-waitlist`

Three endpoints:

**POST `/courses/{courseId:guid}/walkup-waitlist/open`**
- No request body
- Validates course exists (404 if not)
- Checks for existing waitlist for today with status "Open" or "Closed" (409 Conflict if already exists for today — one session per day)
- Generates globally unique 4-digit short code for today
- Creates `CourseWaitlist` with Status="Open", OpenedAt=now
- Returns 201 Created with response body:

```json
{
  "id": "guid",
  "courseId": "guid",
  "shortCode": "1234",
  "date": "2026-03-05",
  "status": "Open",
  "openedAt": "2026-03-05T10:00:00Z",
  "closedAt": null
}
```

**POST `/courses/{courseId:guid}/walkup-waitlist/close`**
- No request body
- Validates course exists (404 if not)
- Finds today's waitlist with status "Open" (404 if no open waitlist for today)
- Sets Status="Closed", ClosedAt=now, UpdatedAt=now
- Returns 200 OK with updated waitlist response (same shape as open)

**GET `/courses/{courseId:guid}/walkup-waitlist/today`**
- Validates course exists (404 if not)
- Returns today's waitlist if it exists, with an empty entries array (golfer entries come in a future story)
- If no waitlist exists for today, returns 200 OK with:

```json
{
  "waitlist": null,
  "entries": []
}
```

- If waitlist exists, returns 200 OK with:

```json
{
  "waitlist": {
    "id": "guid",
    "courseId": "guid",
    "shortCode": "1234",
    "date": "2026-03-05",
    "status": "Open",
    "openedAt": "2026-03-05T10:00:00Z",
    "closedAt": null
  },
  "entries": []
}
```

The `entries` array is always empty for now — it will be populated when the golfer-join story is built (future `GolferWaitlistEntry` entity). The frontend should handle this gracefully (empty state).

### 3.2 Files to Modify

#### `src/api/Data/ApplicationDbContext.cs`

- Add `DbSet<CourseWaitlist> CourseWaitlists` property
- Add EF configuration in `OnModelCreating` (see section 2.3)

#### `src/api/Models/Course.cs`

- Add navigation property: `public ICollection<CourseWaitlist>? Waitlists { get; set; }`

#### `src/api/Program.cs`

- Add `app.MapWalkUpWaitlistEndpoints();` after the existing endpoint registrations

### 3.3 Request/Response Records

Define in `WalkUpWaitlistEndpoints.cs` (follows the existing pattern where records are in the same file as the endpoint class):

```
public record WalkUpWaitlistResponse(
    Guid Id,
    Guid CourseId,
    string ShortCode,
    string Date,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record WalkUpWaitlistTodayResponse(
    WalkUpWaitlistResponse? Waitlist,
    List<WalkUpWaitlistEntryResponse> Entries);

public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    DateTimeOffset JoinedAt);
```

`WalkUpWaitlistEntryResponse` is defined now for the response shape but will always be an empty list until the golfer-join story is implemented.

### 3.4 Validation and Error Handling

| Scenario | Status Code | Error Message |
|----------|-------------|---------------|
| Course not found | 404 | "Course not found." |
| Waitlist already opened for today (open) | 409 | "Walk-up waitlist is already open for today." |
| Waitlist already opened for today (closed) | 409 | "Walk-up waitlist was already used today." |
| No open waitlist to close | 404 | "No open walk-up waitlist found for today." |
| Short code generation failed (exhausted retries) | 500 | "Unable to generate a unique short code. Please try again." |

Error responses use the existing `new { error = "..." }` pattern.

### 3.5 Multi-Tenancy

The endpoints take `courseId` as a route parameter. The first operation in each endpoint is `db.Courses.FirstOrDefaultAsync(c => c.Id == courseId)`, which is automatically scoped by the tenant query filter on `Course`. This means an operator from Tenant A cannot open/close/view the waitlist for a course belonging to Tenant B. No additional tenant filtering is needed on `CourseWaitlist` queries as long as they go through a validated `courseId`.

### 3.6 Date Handling

"Today" is determined as `DateOnly.FromDateTime(DateTime.UtcNow)`. This is UTC-based. In a future iteration, this could be course-timezone-aware, but for v1 UTC is acceptable (the operator is actively using the system during their business day, so the date boundary edge case is minimal).

---

## 4. Frontend Implementation

### 4.1 Files to Create

#### `src/web/src/types/waitlist.ts`

TypeScript interfaces matching the API response shapes:

```typescript
export interface WalkUpWaitlist {
  id: string;
  courseId: string;
  shortCode: string;
  date: string;
  status: 'Open' | 'Closed';
  openedAt: string;
  closedAt: string | null;
}

export interface WalkUpWaitlistEntry {
  id: string;
  golferName: string;
  joinedAt: string;
}

export interface WalkUpWaitlistTodayResponse {
  waitlist: WalkUpWaitlist | null;
  entries: WalkUpWaitlistEntry[];
}
```

#### `src/web/src/features/operator/hooks/useWalkUpWaitlist.ts`

Three hooks:

```typescript
// Query: GET today's waitlist status
export function useWalkUpWaitlistToday(courseId: string | undefined)
  -> useQuery with queryKey: queryKeys.walkUpWaitlist.today(courseId)
  -> queryFn: api.get<WalkUpWaitlistTodayResponse>(`/courses/${courseId}/walkup-waitlist/today`)
  -> enabled: !!courseId
  -> refetchInterval: 30000 (30s polling for near-real-time queue updates)

// Mutation: POST open
export function useOpenWalkUpWaitlist()
  -> useMutation
  -> mutationFn: ({ courseId }) => api.post<WalkUpWaitlist>(`/courses/${courseId}/walkup-waitlist/open`, {})
  -> onSuccess: invalidate queryKeys.walkUpWaitlist.today(courseId)

// Mutation: POST close
export function useCloseWalkUpWaitlist()
  -> useMutation
  -> mutationFn: ({ courseId }) => api.post<WalkUpWaitlist>(`/courses/${courseId}/walkup-waitlist/close`, {})
  -> onSuccess: invalidate queryKeys.walkUpWaitlist.today(courseId)
```

#### `src/web/src/features/operator/pages/WalkUpWaitlist.tsx`

Main page component. Default export. Renders different views based on state:

**State 1: Loading**
- Show skeleton/loading indicator while `useWalkUpWaitlistToday` is loading

**State 2: Inactive (no waitlist for today)**
- Card with title "Walk-Up Waitlist"
- Description: "Open the waitlist to generate a short code for walk-up golfers."
- Primary Button: "Open Waitlist" (calls `useOpenWalkUpWaitlist`)
- Disabled while mutation is pending

**State 3: Active — Open (empty queue)**
- Card header: "Walk-Up Waitlist" with Badge variant="default" showing "Open" (green)
- Short code display: centered `text-6xl font-mono tracking-widest` (e.g., "4 8 2 7" with spaces for readability)
- Copy button: outline Button, label toggles "Copy Code" -> "Copied!" for 2 seconds using local `useState` + `setTimeout`
  - Uses `navigator.clipboard.writeText(shortCode)`
- Queue section: "0 golfers in queue" text
- Empty state message: "No golfers have joined yet. Share the short code with walk-up golfers."
- Close button: outline Button with destructive text color at bottom

**State 4: Active — Open (with golfers)**
- Same as State 3 but:
- Queue count: "N golfer(s) in queue" (bold)
- shadcn Table with columns: # (position), Name, Joined At (relative time like "5 min ago")
- Note: This state will not be reachable until the golfer-join story is built, but the UI should handle it gracefully

**State 5: Closed**
- Card header: "Walk-Up Waitlist" with Badge variant="secondary" showing "Closed"
- Short code displayed at reduced prominence (muted text, smaller)
- Message: "The waitlist was closed at {closedAt time}."
- Queue preserved (read-only Table if entries exist)
- No action buttons (waitlist already used for today)

**Close confirmation:**
- AlertDialog (already available in `@/components/ui/alert-dialog`)
- Title: "Close Walk-Up Waitlist?"
- Description: "No new golfers will be able to join. Existing entries will be preserved."
- Cancel button: "Keep Open" (autoFocus — safe action first)
- Action button: "Close Waitlist" (destructive variant)

**Mobile responsive:**
- Short code scales to `text-4xl` on small screens (`text-4xl sm:text-6xl`)
- Table collapses: on `md:` screens show full table, on mobile show stacked cards (each entry as a small card with name + time)

**Error handling:**
- If open mutation returns 409: show inline error "Waitlist is already open for today."
- If close mutation returns 404: show inline error (should not happen in normal flow)
- General mutation errors: show error message from the API response

### 4.2 Files to Modify

#### `src/web/src/lib/query-keys.ts`

Add:
```typescript
walkUpWaitlist: {
  today: (courseId: string) => ['walkup-waitlist', courseId, 'today'] as const,
},
```

#### `src/web/src/features/operator/index.tsx`

Add route inside the `CourseGate` component's `<Routes>`:
```tsx
<Route path="waitlist" element={<WalkUpWaitlist />} />
```

Import `WalkUpWaitlist` from `./pages/WalkUpWaitlist`.

#### `src/web/src/components/layout/OperatorLayout.tsx`

Add a new `SidebarMenuItem` for the waitlist nav item, placed after "Tee Sheet" and before "Settings":

```tsx
<SidebarMenuItem>
  <SidebarMenuButton asChild>
    <NavLink to="/operator/waitlist">
      {({ isActive }) => (
        <span className={isActive ? 'font-semibold' : ''}>Waitlist</span>
      )}
    </NavLink>
  </SidebarMenuButton>
</SidebarMenuItem>
```

---

## 5. Test Strategy

### 5.1 Backend Integration Tests

#### Create: `tests/api/WalkUpWaitlistEndpointsTests.cs`

Test class: `WalkUpWaitlistEndpointsTests : IClassFixture<TestWebApplicationFactory>`

Follow the existing test pattern from `CourseEndpointsTests.cs`:
- Private helper `CreateTestTenantAsync()` and `CreateTestCourseAsync(tenantId)`
- Each test creates its own tenant + course to avoid cross-test interference
- Use `X-Tenant-Id` header on all requests

**Tests:**

1. **Open_ReturnsCreated_WithShortCode** — POST open, assert 201, response has 4-digit shortCode, status "Open", openedAt set
2. **Open_WhenAlreadyOpen_Returns409** — POST open twice, second returns 409
3. **Open_WhenAlreadyClosed_Returns409** — Open then close then open again, third returns 409 (one session per day)
4. **Open_CourseNotFound_Returns404** — POST open with random Guid courseId
5. **Open_ShortCodeIsFourDigits** — Assert shortCode matches regex `^\d{4}$`
6. **Open_ShortCodeIsUniquePerDay** — Open waitlists for two different courses, assert different short codes
7. **Close_ReturnsOk_WithClosedStatus** — Open then close, assert 200, status "Closed", closedAt set
8. **Close_WhenNoOpenWaitlist_Returns404** — POST close without opening first
9. **Close_CourseNotFound_Returns404** — POST close with random Guid
10. **Today_WhenNoWaitlist_ReturnsNullWaitlist** — GET today, assert 200, waitlist is null, entries is empty
11. **Today_WhenOpen_ReturnsWaitlistWithEmptyEntries** — Open then GET today, assert waitlist present with status "Open"
12. **Today_WhenClosed_ReturnsClosedWaitlist** — Open, close, GET today, assert status "Closed"
13. **Today_CourseNotFound_Returns404** — GET today with random Guid
14. **TenantIsolation_CannotAccessOtherTenantWaitlist** — Tenant A opens waitlist for Course A, Tenant B tries to GET today for Course A, gets 404 (query filter blocks it)

**Response records for deserialization** (private in test class):
```
private record WalkUpWaitlistResponse(Guid Id, Guid CourseId, string ShortCode, string Date, string Status, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt);
private record WalkUpWaitlistTodayResponse(WalkUpWaitlistResponse? Waitlist, List<object> Entries);
```

### 5.2 Frontend Component Tests

#### Create: `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`

Mock `useWalkUpWaitlistToday`, `useOpenWalkUpWaitlist`, `useCloseWalkUpWaitlist` hooks.

Also mock `useCourseContext` to return a selected course.

**Tests:**

1. **renders loading state** — mock query as loading, assert loading indicator present
2. **renders inactive state with open button** — mock query returning null waitlist, assert "Open Waitlist" button present
3. **renders open state with short code** — mock query returning open waitlist with shortCode "4827", assert code displayed, "Copy Code" button visible
4. **renders closed state** — mock query returning closed waitlist, assert "Closed" badge, no action buttons
5. **calls open mutation on button click** — click "Open Waitlist", assert mutation called
6. **shows close confirmation dialog** — click close button, assert AlertDialog appears with "Keep Open" and "Close Waitlist"
7. **calls close mutation on confirm** — click through AlertDialog confirm, assert close mutation called
8. **renders empty queue message when open with no entries** — assert "No golfers have joined yet" text
9. **shows 409 error when already open** — mock open mutation error with status 409, assert error message displayed

---

## 6. Integration Points

### 6.1 Dependencies on Existing Code

| File | Dependency |
|------|-----------|
| `ApplicationDbContext.cs` | Add new DbSet + config |
| `Course.cs` | Add navigation property |
| `Program.cs` | Register new endpoint mapping |
| `OperatorLayout.tsx` | Add nav item |
| `operator/index.tsx` | Add route |
| `query-keys.ts` | Add walkUpWaitlist key factory |
| `api-client.ts` | Used as-is (no changes needed) |

### 6.2 Future Integration

- **Golfer join story:** Will add `GolferWaitlistEntry` entity with FK to `CourseWaitlist`. The `GET /today` endpoint will be extended to populate the `entries` array by querying `GolferWaitlistEntry WHERE CourseWaitlistId = X AND RemovedAt IS NULL ORDER BY JoinedAt ASC`.
- **Short code lookup:** A golfer-facing endpoint like `POST /waitlist/join` will accept a short code, find the matching `CourseWaitlist` for today, and create a `GolferWaitlistEntry`.
- **Event publishing:** When the golfer-join story adds entries, the operator queue view will benefit from the 30s polling already configured. If real-time is needed later, SSE or WebSocket can replace polling.

---

## 7. Implementation Order

1. **Entity + Migration** — `CourseWaitlist.cs`, modify `Course.cs`, `ApplicationDbContext.cs`, run `dotnet ef migrations add AddCourseWaitlist`
2. **Endpoints** — `WalkUpWaitlistEndpoints.cs`, register in `Program.cs`
3. **Backend tests** — `WalkUpWaitlistEndpointsTests.cs`, verify all pass
4. **Frontend types + hooks** — `waitlist.ts`, `query-keys.ts`, `useWalkUpWaitlist.ts`
5. **Frontend page** — `WalkUpWaitlist.tsx`, add route and nav item
6. **Frontend tests** — `WalkUpWaitlist.test.tsx`

---

## 8. Risks and Open Questions

### Risks

1. **Date boundary (UTC vs local):** "Today" is UTC-based. A course in Hawaii (UTC-10) at 8pm local time would see tomorrow's date in UTC. For v1, this is acceptable since operators actively manage walk-up waitlists during business hours which overlap with UTC dates. The risk is minimal and a timezone-aware solution can be added later when Course gains a timezone field.

2. **Short code collision probability:** With 10,000 possible codes and likely <100 active courses per day, the birthday paradox gives ~0.05% collision probability. The retry loop (10 attempts) makes failure extremely unlikely. If the platform scales to thousands of courses, short codes could expand to 5 digits.

3. **SQLite test compatibility:** `DateOnly` is used, which SQLite handles via EF Core's value converter. The unique constraint on `(CourseId, Date)` should work in both SQLite (tests) and SQL Server (production). The `HasMaxLength` on `ShortCode` also translates correctly.

### Resolved Decisions

- **No `WaitlistEnabled` flag:** Per the issue, walk-up waitlist is always enabled. No feature flag needed. This diverges from the architecture doc's `Course.WaitlistEnabled` — that flag may still be introduced for the remote/SMS waitlist (#3) but is not needed here.
- **One session per day:** An operator cannot re-open a waitlist after closing it. If they need to, they must wait until tomorrow. This is intentional — it prevents confusion with golfers who saw the old short code.
- **No request body for open/close:** These are simple state transitions. The system generates the short code; the operator does not choose it.

### No Blocking Questions

All acceptance criteria are clear. No escalation needed.
