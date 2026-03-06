# Issue #180 — Walk-up Waitlist: Operator Trigger

## Technical Plan

### Approach

Add a unified waitlist feature to the operator dashboard. The backend introduces two new entities (`CourseWaitlist`, `WaitlistRequest`), a `WaitlistEnabled` feature flag on `Course`, a domain event infrastructure (`IDomainEvent`, `IDomainEventPublisher`, `IDomainEventHandler<T>`, `InProcessDomainEventPublisher`), and four API endpoints. The frontend adds a new Waitlist page with summary stats, an inline add-entry form, and an entries table, plus a sidebar nav item. The tee time field is a free-form time input (not API-populated from tee sheet settings) since courses should be able to use the waitlist without configuring tee sheet settings.

### Implementation Order

Build in this sequence. Each step depends on the previous:

1. **Backend: Domain event infrastructure** (interfaces + in-process publisher)
2. **Backend: Data model** (entities + DbContext + migration + Course.WaitlistEnabled)
3. **Backend: API endpoints** (waitlist settings + GET waitlist + POST request)
4. **Backend: Integration tests**
5. **Frontend: Types + query keys + API hooks**
6. **Frontend: Waitlist page component**
7. **Frontend: Navigation + routing integration**
8. **Frontend: Component tests**

---

## 1. Backend Changes

### 1.1 Domain Event Infrastructure

#### Create: `src/api/Events/IDomainEvent.cs`

```
namespace Shadowbrook.Api.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
```

#### Create: `src/api/Events/IDomainEventPublisher.cs`

```
namespace Shadowbrook.Api.Events;

public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
```

#### Create: `src/api/Events/IDomainEventHandler.cs`

```
namespace Shadowbrook.Api.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
```

#### Create: `src/api/Events/InProcessDomainEventPublisher.cs`

Resolves all `IDomainEventHandler<TEvent>` from the DI container. Calls each handler sequentially. Catches and logs exceptions from handlers without failing the parent operation (per project principle: "If a downstream system is slow or down, the core flow still completes").

```
namespace Shadowbrook.Api.Events;

public class InProcessDomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessDomainEventPublisher> _logger;

    // constructor injection

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(typeof(TEvent));
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            try
            {
                await ((IDomainEventHandler<TEvent>)handler!).HandleAsync(domainEvent, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event handler {Handler} failed for {Event}",
                    handler!.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}
```

#### Create: `src/api/Events/WaitlistRequestCreated.cs`

```
namespace Shadowbrook.Api.Events;

public record WaitlistRequestCreated : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistRequestId { get; init; }
    public required Guid CourseWaitlistId { get; init; }
    public required Guid CourseId { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
    public required int GolfersNeeded { get; init; }
}
```

#### Modify: `src/api/Program.cs`

Register the event infrastructure:

```csharp
builder.Services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();
```

Add `using Shadowbrook.Api.Events;` to imports. Add this registration after the existing `AddScoped<ITextMessageService, ...>()` line.

### 1.2 Data Model

#### Create: `src/api/Models/CourseWaitlist.cs`

```
namespace Shadowbrook.Api.Models;

public class CourseWaitlist
{
    public Guid Id { get; set; }
    public required Guid CourseId { get; set; }
    public required DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Course? Course { get; set; }
    public ICollection<WaitlistRequest> WaitlistRequests { get; set; } = new List<WaitlistRequest>();
}
```

#### Create: `src/api/Models/WaitlistRequest.cs`

```
namespace Shadowbrook.Api.Models;

public class WaitlistRequest
{
    public Guid Id { get; set; }
    public required Guid CourseWaitlistId { get; set; }
    public required TimeOnly TeeTime { get; set; }
    public required int GolfersNeeded { get; set; }
    public required string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public CourseWaitlist? CourseWaitlist { get; set; }
}
```

#### Modify: `src/api/Models/Course.cs`

Add after the `FlatRatePrice` property:

```csharp
public bool? WaitlistEnabled { get; set; }
```

Add navigation property after the `Tenant` navigation:

```csharp
public ICollection<CourseWaitlist> CourseWaitlists { get; set; } = new List<CourseWaitlist>();
```

#### Modify: `src/api/Data/ApplicationDbContext.cs`

Add DbSet properties after `Bookings`:

```csharp
public DbSet<CourseWaitlist> CourseWaitlists => Set<CourseWaitlist>();
public DbSet<WaitlistRequest> WaitlistRequests => Set<WaitlistRequest>();
```

Add entity configuration inside `OnModelCreating`, after the `Booking` configuration:

```csharp
// CourseWaitlist configuration
modelBuilder.Entity<CourseWaitlist>()
    .HasOne(cw => cw.Course)
    .WithMany(c => c.CourseWaitlists)
    .HasForeignKey(cw => cw.CourseId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<CourseWaitlist>()
    .HasIndex(cw => new { cw.CourseId, cw.Date })
    .IsUnique();

// WaitlistRequest configuration
modelBuilder.Entity<WaitlistRequest>()
    .HasOne(wr => wr.CourseWaitlist)
    .WithMany(cw => cw.WaitlistRequests)
    .HasForeignKey(wr => wr.CourseWaitlistId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<WaitlistRequest>()
    .HasIndex(wr => new { wr.CourseWaitlistId, wr.TeeTime });

modelBuilder.Entity<WaitlistRequest>()
    .HasIndex(wr => new { wr.CourseWaitlistId, wr.Status });
```

#### EF Migration

After the model changes compile successfully, run:

```bash
export PATH="$PATH:/home/aaron/.dotnet/tools"
dotnet ef migrations add AddWaitlistEntities --project src/api
```

Migration name: `AddWaitlistEntities`. This creates:
- `CourseWaitlists` table with unique index on `(CourseId, Date)`
- `WaitlistRequests` table with indexes on `(CourseWaitlistId, TeeTime)` and `(CourseWaitlistId, Status)`
- `WaitlistEnabled` nullable bool column on `Courses` table

Review the generated `Up()` and `Down()` methods for correctness.

### 1.3 API Endpoints

#### Create: `src/api/Endpoints/WaitlistEndpoints.cs`

Follow the same extension method pattern as `CourseEndpoints.cs`. All endpoints are nested under `/courses/{courseId}/...` to maintain course-scoped tenancy.

```csharp
namespace Shadowbrook.Api.Endpoints;

public static class WaitlistEndpoints
{
    public static void MapWaitlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/courses/{courseId:guid}");

        group.MapGet("waitlist-settings", GetWaitlistSettings);
        group.MapPut("waitlist-settings", UpdateWaitlistSettings);
        group.MapGet("waitlist", GetWaitlist);
        group.MapPost("waitlist/requests", CreateWaitlistRequest);
    }

    // ... endpoint methods below
}
```

**Important:** Register in `Program.cs` after `app.MapTeeSheetEndpoints();`:

```csharp
app.MapWaitlistEndpoints();
```

---

#### Endpoint 1: `GET /courses/{courseId}/waitlist-settings`

**Purpose:** Return the waitlist feature state for a course.

**Logic:**
1. Look up the course by `courseId`. Return 404 if not found.
2. Return `{ waitlistEnabled: bool }`. If `WaitlistEnabled` is null, return `false` (not yet configured = disabled).

**Response DTO:**

```csharp
public record WaitlistSettingsResponse(bool WaitlistEnabled);
```

**Status codes:** 200 OK, 404 Not Found.

---

#### Endpoint 2: `PUT /courses/{courseId}/waitlist-settings`

**Purpose:** Enable or disable the waitlist feature for a course.

**Request DTO:**

```csharp
public record WaitlistSettingsRequest(bool WaitlistEnabled);
```

**Logic:**
1. Look up the course by `courseId`. Return 404 if not found.
2. Set `course.WaitlistEnabled = request.WaitlistEnabled`.
3. Set `course.UpdatedAt = DateTimeOffset.UtcNow`.
4. Save changes.
5. Return `WaitlistSettingsResponse`.

**Status codes:** 200 OK, 404 Not Found.

---

#### Endpoint 3: `GET /courses/{courseId}/waitlist?date=yyyy-MM-dd`

**Purpose:** Return waitlist requests and summary for a course on a given date.

**Logic:**
1. Validate `date` query parameter (required, must parse as `yyyy-MM-dd`). Return 400 if invalid.
2. Look up the course by `courseId`. Return 404 if not found.
3. Check `course.WaitlistEnabled`. If not `true`, return 400 with `{ error: "Waitlist is not enabled for this course." }`.
4. Look up `CourseWaitlist` for this `courseId` and `date`. If none exists, return an empty response (no entries for this date yet).
5. Fetch all `WaitlistRequest` records for the `CourseWaitlist`, ordered by `TeeTime` ascending.
6. Calculate `totalGolfersPending` = sum of `GolfersNeeded` for all requests with status `"Pending"`.
7. Return response.

**Response DTOs:**

```csharp
public record WaitlistResponse(
    Guid? CourseWaitlistId,
    string Date,
    int TotalGolfersPending,
    List<WaitlistRequestResponse> Requests);

public record WaitlistRequestResponse(
    Guid Id,
    string TeeTime,       // "HH:mm" format
    int GolfersNeeded,
    string Status);
```

When no `CourseWaitlist` exists for the date, return:
```json
{ "courseWaitlistId": null, "date": "2026-03-02", "totalGolfersPending": 0, "requests": [] }
```

**Status codes:** 200 OK, 400 Bad Request, 404 Not Found.

---

#### Endpoint 4: `POST /courses/{courseId}/waitlist/requests`

**Purpose:** Operator adds a tee time to the waitlist.

**Request DTO:**

```csharp
public record CreateWaitlistRequestRequest(string TeeTime, int GolfersNeeded);
```

**Logic:**
1. Look up the course by `courseId`. Return 404 if not found.
2. Check `course.WaitlistEnabled`. If not `true`, return 400 with `{ error: "Waitlist is not enabled for this course." }`.
3. Validate `TeeTime`: must parse as `TimeOnly` from `"HH:mm"` format. Return 400 if invalid.
4. Validate `GolfersNeeded`: must be between 1 and 4 (inclusive). Return 400 with `{ error: "Golfers needed must be between 1 and 4." }` if invalid.
5. Parse `date` from the request body. **Important design decision:** The request body should also include a `date` field to know which day the request is for. Update the request DTO:

```csharp
public record CreateWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);
```

6. Validate `Date`: must parse as `DateOnly` from `"yyyy-MM-dd"`. Return 400 if invalid.
7. Find or create `CourseWaitlist` for this `courseId` and parsed date (upsert pattern):
   - Query `db.CourseWaitlists.FirstOrDefaultAsync(cw => cw.CourseId == courseId && cw.Date == date)`.
   - If null, create a new `CourseWaitlist` with `Id = Guid.NewGuid()`, `CourseId = courseId`, `Date = date`, timestamps.
8. Check for existing active request: query `WaitlistRequests` where `CourseWaitlistId` matches, `TeeTime` matches, and `Status` is `"Pending"`. If found, return 409 Conflict with `{ error: "An active waitlist request already exists for this tee time." }`.
9. Create `WaitlistRequest` with `Id = Guid.NewGuid()`, `CourseWaitlistId`, `TeeTime`, `GolfersNeeded`, `Status = "Pending"`, timestamps.
10. Add to DbContext and `SaveChangesAsync()`.
11. Publish `WaitlistRequestCreated` event via `IDomainEventPublisher` (after save). No handlers exist yet, so this is a no-op for now.
12. Return 201 Created with the created request details and a Location header.

**Response:** Return a `WaitlistRequestResponse` record (same shape as in the GET response).

**Status codes:** 201 Created, 400 Bad Request, 404 Not Found, 409 Conflict.

---

### 1.4 Records Summary (all in `WaitlistEndpoints.cs`)

Place all request/response records at the bottom of `WaitlistEndpoints.cs`, following the pattern established in `CourseEndpoints.cs`:

```csharp
public record WaitlistSettingsRequest(bool WaitlistEnabled);
public record WaitlistSettingsResponse(bool WaitlistEnabled);
public record CreateWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);
public record WaitlistResponse(
    Guid? CourseWaitlistId,
    string Date,
    int TotalGolfersPending,
    List<WaitlistRequestResponse> Requests);
public record WaitlistRequestResponse(
    Guid Id,
    string TeeTime,
    int GolfersNeeded,
    string Status);
```

---

## 2. Frontend Changes

### 2.1 Types

#### Create: `src/web/src/types/waitlist.ts`

```typescript
export interface WaitlistSettings {
  waitlistEnabled: boolean;
}

export interface WaitlistRequestEntry {
  id: string;
  teeTime: string;      // "HH:mm"
  golfersNeeded: number;
  status: string;
}

export interface WaitlistResponse {
  courseWaitlistId: string | null;
  date: string;
  totalGolfersPending: number;
  requests: WaitlistRequestEntry[];
}

export interface CreateWaitlistRequest {
  date: string;
  teeTime: string;
  golfersNeeded: number;
}
```

### 2.2 Query Keys

#### Modify: `src/web/src/lib/query-keys.ts`

Add a new `waitlist` section:

```typescript
waitlist: {
  byDate: (courseId: string, date: string) =>
    ['waitlist', courseId, date] as const,
  settings: (courseId: string) =>
    ['waitlist', courseId, 'settings'] as const,
},
```

Add this after the `teeSheets` key group.

### 2.3 API Hooks

#### Create: `src/web/src/features/operator/hooks/useWaitlist.ts`

Contains three hooks following the pattern in `useTeeTimeSettings.ts`:

**`useWaitlistSettings(courseId)`** -- fetches waitlist settings for the course.

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { WaitlistSettings, WaitlistResponse, CreateWaitlistRequest, WaitlistRequestEntry } from '@/types/waitlist';

export function useWaitlistSettings(courseId: string | undefined) {
  return useQuery({
    queryKey: courseId ? queryKeys.waitlist.settings(courseId) : ['disabled'],
    queryFn: () => api.get<WaitlistSettings>(`/courses/${courseId}/waitlist-settings`),
    enabled: !!courseId,
  });
}
```

**`useWaitlist(courseId, date)`** -- fetches waitlist entries for a course and date. Only enabled when courseId exists AND settings confirm waitlist is enabled (pass `enabled` flag from parent).

```typescript
export function useWaitlist(courseId: string | undefined, date: string, enabled: boolean = true) {
  return useQuery({
    queryKey: courseId ? queryKeys.waitlist.byDate(courseId, date) : ['disabled'],
    queryFn: () => api.get<WaitlistResponse>(`/courses/${courseId}/waitlist?date=${date}`),
    enabled: !!courseId && enabled,
  });
}
```

**`useCreateWaitlistRequest()`** -- mutation to create a waitlist request.

```typescript
export function useCreateWaitlistRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, data }: { courseId: string; data: CreateWaitlistRequest }) =>
      api.post<WaitlistRequestEntry>(`/courses/${courseId}/waitlist/requests`, data),
    onSuccess: (_, { courseId, data }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.waitlist.byDate(courseId, data.date) });
    },
  });
}
```

### 2.4 Waitlist Page

#### Create: `src/web/src/features/operator/pages/Waitlist.tsx`

This is the main page component. Follow the layout pattern from `TeeSheet.tsx` and `TeeTimeSettings.tsx`.

**Page structure:**

```
<div className="p-6">
  <!-- Page header -->
  <h1>Waitlist</h1>
  <p>(subtitle)</p>

  <!-- No course selected state -->
  <!-- Feature disabled state -->
  <!-- Loading state -->
  <!-- Error state -->

  <!-- Main content (when loaded and enabled) -->
    <!-- Summary stat card -->
    <!-- Inline add-entry form -->
    <!-- Entries table -->
</div>
```

**Detailed component behavior:**

1. **Setup:**
   - Get `course` from `useCourseContext()`.
   - If no course, render centered "Select a course" message (same as TeeSheet.tsx line 20-26).
   - State: `selectedDate` with `useState<string>` defaulting to today (`getTodayDate()` -- reuse the same helper from TeeSheet or extract to a shared utility).
   - Fetch `useWaitlistSettings(course?.id)`.
   - Derive `waitlistEnabled = settingsQuery.data?.waitlistEnabled === true`.
   - Fetch `useWaitlist(course?.id, selectedDate, waitlistEnabled)` -- only when waitlist is enabled.
   - Setup `useCreateWaitlistRequest()` mutation.

2. **Feature Disabled state:**
   When settings have loaded and `waitlistEnabled` is false, show a dashed-border callout:
   ```tsx
   <div className="mt-6 rounded-lg border-2 border-dashed border-muted-foreground/25 p-6 text-center">
     <p className="text-muted-foreground">Waitlist is not enabled for this course.</p>
     <p className="mt-1 text-sm text-muted-foreground">Enable the waitlist in course settings to use this feature.</p>
   </div>
   ```
   Do NOT render the form or table when disabled.

3. **Loading state:**
   When `settingsQuery.isLoading` or (when enabled) `waitlistQuery.isLoading`, show Skeleton placeholders:
   - One Skeleton for the summary card area: `<Skeleton className="h-24 w-full max-w-xs" />`
   - Multiple Skeletons for table rows: 3-4 `<Skeleton className="h-10 w-full" />` blocks.

4. **Summary stat Card:**
   Uses the `Card`, `CardHeader`, `CardTitle`, `CardContent` components:
   ```tsx
   <Card className="w-fit">
     <CardHeader>
       <CardTitle className="text-sm font-medium text-muted-foreground">
         Total Golfers Pending
       </CardTitle>
     </CardHeader>
     <CardContent>
       <p className="text-3xl font-bold">{waitlistData.totalGolfersPending}</p>
     </CardContent>
   </Card>
   ```

5. **Date picker:**
   Simple HTML date input matching the TeeSheet pattern (line 38-46 of TeeSheet.tsx):
   ```tsx
   <div className="space-y-2">
     <label htmlFor="waitlist-date" className="text-sm font-medium">Date</label>
     <input
       id="waitlist-date"
       type="date"
       value={selectedDate}
       onChange={(e) => setSelectedDate(e.target.value)}
       className="flex h-9 w-full max-w-xs rounded-md border border-input bg-background px-3 py-1 text-base shadow-sm ..."
     />
   </div>
   ```

6. **Inline add-entry form:**
   Uses React Hook Form + Zod. The form is inline (not a dialog/modal).

   **Zod schema:**
   ```typescript
   import { z } from 'zod/v4';

   const addWaitlistRequestSchema = z.object({
     teeTime: z.string().min(1, 'Tee time is required'),
     golfersNeeded: z.number().min(1, 'At least 1 golfer needed').max(4, 'Maximum 4 golfers'),
   });
   ```

   **Form fields (horizontal on desktop, stacked on mobile):**
   ```tsx
   <div className="mt-6 flex flex-col gap-4 md:flex-row md:items-end">
   ```

   - **Tee Time:** Use an `<Input type="time" />` field (not a Select populated by the API). Per the interaction spec, courses should be able to use the waitlist without having tee sheet settings configured. The time input gives free-form entry.
     ```tsx
     <FormField
       control={form.control}
       name="teeTime"
       render={({ field }) => (
         <FormItem>
           <FormLabel>Tee Time</FormLabel>
           <FormControl>
             <Input type="time" {...field} />
           </FormControl>
           <FormMessage />
         </FormItem>
       )}
     />
     ```

   - **Golfers Needed:** Use a `<Select>` with values 1-4.
     ```tsx
     <FormField
       control={form.control}
       name="golfersNeeded"
       render={({ field }) => (
         <FormItem>
           <FormLabel>Golfers Needed</FormLabel>
           <Select
             value={String(field.value)}
             onValueChange={(v) => field.onChange(Number(v))}
           >
             <FormControl>
               <SelectTrigger className="w-[120px]">
                 <SelectValue placeholder="Select" />
               </SelectTrigger>
             </FormControl>
             <SelectContent>
               <SelectItem value="1">1</SelectItem>
               <SelectItem value="2">2</SelectItem>
               <SelectItem value="3">3</SelectItem>
               <SelectItem value="4">4</SelectItem>
             </SelectContent>
           </Select>
           <FormMessage />
         </FormItem>
       )}
     />
     ```

   - **Submit button:**
     ```tsx
     <Button type="submit" disabled={createMutation.isPending}>
       {createMutation.isPending ? 'Adding...' : 'Add to Waitlist'}
     </Button>
     ```

   **On submit:** Call `createMutation.mutate({ courseId: course.id, data: { date: selectedDate, teeTime: data.teeTime, golfersNeeded: data.golfersNeeded } })`. On success, reset the form. Handle 409 conflict errors by showing inline error.

   **Success/error messages below the form:**
   ```tsx
   {createMutation.isSuccess && (
     <p className="text-sm text-green-600" role="status">
       Tee time added to waitlist.
     </p>
   )}
   {createMutation.isError && (
     <p className="text-sm text-destructive" role="alert">
       {createMutation.error.message}
     </p>
   )}
   ```

7. **Entries Table:**
   Uses shadcn Table components, matching the TeeSheet pattern:

   ```tsx
   <Table>
     <TableHeader>
       <TableRow>
         <TableHead>Tee Time</TableHead>
         <TableHead>Golfers Needed</TableHead>
         <TableHead>Status</TableHead>
       </TableRow>
     </TableHeader>
     <TableBody>
       {waitlistData.requests.map((request) => (
         <TableRow key={request.id}>
           <TableCell className="font-semibold">
             {formatTime(request.teeTime)}
           </TableCell>
           <TableCell>{request.golfersNeeded}</TableCell>
           <TableCell>
             <Badge variant="muted">
               {request.golfersNeeded} pending
             </Badge>
           </TableCell>
         </TableRow>
       ))}
     </TableBody>
   </Table>
   ```

   **Empty state:** When `requests` array is empty, show a callout instead of the table:
   ```tsx
   <div className="mt-6 text-center text-muted-foreground">
     No waitlist entries for this date.
   </div>
   ```

8. **`formatTime` helper:** Reuse the same implementation from TeeSheet.tsx (lines 122-130). Either extract to a shared utility in `@/lib/utils.ts` or duplicate in this component. Recommend duplicating for now (matches the existing pattern where TeeSheet has its own helpers).

### 2.5 Navigation + Routing

#### Modify: `src/web/src/components/layout/OperatorLayout.tsx`

Add a "Waitlist" nav item between "Tee Sheet" and "Settings" in the `<SidebarMenu>`. Insert after the "Tee Sheet" `<SidebarMenuItem>` (after line 52):

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

#### Modify: `src/web/src/features/operator/index.tsx`

1. Add import at top:
   ```tsx
   import Waitlist from './pages/Waitlist';
   ```

2. Add route in the `CourseGate` component's Routes (inside the course-selected block), after the tee-sheet route (after line 37):
   ```tsx
   <Route path="waitlist" element={<Waitlist />} />
   ```

---

## 3. Testing Strategy

### 3.1 Backend Integration Tests

#### Create: `tests/api/WaitlistEndpointsTests.cs`

Follow the pattern in `TeeSheetEndpointsTests.cs`. Use `IClassFixture<TestWebApplicationFactory>`.

**Helper methods:**
- `CreateTestTenantAsync()` -- same pattern as TeeSheetEndpointsTests
- `CreateTestCourseAsync(tenantId)` -- creates a course and returns its ID
- `EnableWaitlistAsync(courseId)` -- calls `PUT /courses/{courseId}/waitlist-settings` with `{ waitlistEnabled: true }`

**Test response records (private, inside the test class):**
```csharp
private record TenantResponse(Guid Id);
private record CourseResponse(Guid Id, string Name);
private record WaitlistSettingsResponse(bool WaitlistEnabled);
private record WaitlistResponse(Guid? CourseWaitlistId, string Date, int TotalGolfersPending, List<WaitlistRequestResponse> Requests);
private record WaitlistRequestResponse(Guid Id, string TeeTime, int GolfersNeeded, string Status);
```

**Test cases:**

1. **GetWaitlistSettings_DefaultsToFalse** -- New course has waitlist disabled by default. `GET /courses/{id}/waitlist-settings` returns `{ waitlistEnabled: false }`.

2. **UpdateWaitlistSettings_EnablesWaitlist** -- `PUT` with `{ waitlistEnabled: true }` returns 200 with `{ waitlistEnabled: true }`. Subsequent `GET` confirms.

3. **UpdateWaitlistSettings_DisablesWaitlist** -- Enable then disable. Verify the toggle works.

4. **GetWaitlistSettings_CourseNotFound_Returns404** -- Use a random GUID.

5. **GetWaitlist_WaitlistNotEnabled_Returns400** -- Create course (waitlist defaults to disabled), call `GET /courses/{id}/waitlist?date=2026-03-02`. Expect 400 with error message.

6. **GetWaitlist_NoEntries_ReturnsEmptyList** -- Enable waitlist, call GET for a date with no entries. Expect 200 with `totalGolfersPending: 0` and empty `requests` array.

7. **GetWaitlist_WithEntries_ReturnsSummaryAndRequests** -- Create two waitlist requests for the same date, different tee times. Verify `totalGolfersPending` is the sum, and both requests appear in the response ordered by tee time.

8. **GetWaitlist_MissingDate_Returns400** -- Call without `date` query param.

9. **GetWaitlist_InvalidDateFormat_Returns400** -- Call with `date=03-02-2026` (wrong format).

10. **GetWaitlist_CourseNotFound_Returns404** -- Random GUID for courseId.

11. **CreateWaitlistRequest_ValidRequest_Returns201** -- Enable waitlist, POST with valid tee time and golfers needed. Verify 201 response with correct data. Verify subsequent GET includes the new entry.

12. **CreateWaitlistRequest_WaitlistNotEnabled_Returns400** -- POST without enabling waitlist first.

13. **CreateWaitlistRequest_InvalidTeeTime_Returns400** -- POST with `teeTime: "invalid"`.

14. **CreateWaitlistRequest_GolfersNeededOutOfRange_Returns400** -- Test with 0, 5, and -1.

15. **CreateWaitlistRequest_DuplicateTeeTime_Returns409** -- Create a request, then try to create another for the same date and tee time. Expect 409 Conflict.

16. **CreateWaitlistRequest_SameTeeTimeDifferentDate_Returns201** -- Two requests for the same tee time but different dates should both succeed.

17. **CreateWaitlistRequest_CourseNotFound_Returns404**.

18. **CreateWaitlistRequest_MissingDate_Returns400** -- POST without date field.

19. **CreateWaitlistRequest_CreatesCorrespondingCourseWaitlist** -- Verify that a `CourseWaitlist` is lazily created for the date.

20. **GetWaitlist_MultipleRequestsSameDate_CorrectTotalPending** -- Create requests with different `golfersNeeded` values. Verify the sum is correct.

### 3.2 Frontend Component Tests

#### Create: `src/web/src/features/operator/__tests__/Waitlist.test.tsx`

Follow the pattern from `OperatorLayout.test.tsx`. Mock the hooks.

**Mocks needed:**
```typescript
vi.mock('../context/CourseContext');
vi.mock('../hooks/useWaitlist');
```

**Test cases:**

1. **Shows "Select a course" when no course selected** -- Mock `useCourseContext` to return `course: null`.

2. **Shows feature disabled callout when waitlist is not enabled** -- Mock settings to return `{ waitlistEnabled: false }`.

3. **Shows loading skeletons while loading** -- Mock queries as loading.

4. **Shows summary card with total golfers pending** -- Mock waitlist data with a specific total.

5. **Shows entries table with pending badges** -- Mock waitlist data with requests. Verify "N pending" badges appear.

6. **Shows empty state when no entries** -- Mock with empty requests array.

7. **Shows error text when fetch fails** -- Mock query as error state.

8. **Submit button is disabled during mutation** -- Mock `createMutation.isPending` as true.

9. **Shows success message after successful submission** -- Mock `createMutation.isSuccess` as true.

10. **Shows error message after failed submission** -- Mock `createMutation.isError` as true.

---

## 4. Patterns Referenced

| Pattern | Existing Example | Applied To |
|---------|-----------------|------------|
| Endpoint extension method | `CourseEndpoints.MapCourseEndpoints()` | `WaitlistEndpoints.MapWaitlistEndpoints()` |
| Request/response records in endpoint file | Bottom of `CourseEndpoints.cs` | Bottom of `WaitlistEndpoints.cs` |
| Nullable bool feature flag | `Course.TeeTimeIntervalMinutes` (nullable = not configured) | `Course.WaitlistEnabled` |
| TanStack Query hook pattern | `useTeeTimeSettings.ts`, `useTeeSheet.ts` | `useWaitlist.ts` |
| Centralized query keys | `query-keys.ts` | New `waitlist` section |
| RHF + Zod form | `TeeTimeSettings.tsx` | Inline add-entry form |
| Sidebar NavLink | `OperatorLayout.tsx` | "Waitlist" nav item |
| Feature route | `index.tsx` CourseGate routes | `waitlist` route |
| Integration test class | `TeeSheetEndpointsTests.cs` | `WaitlistEndpointsTests.cs` |

---

## 5. Risks

1. **Concurrent waitlist request creation** -- Two operators posting to the same tee time simultaneously could both succeed since the duplicate check is application-level, not a DB constraint. Acceptable for v1 (low frequency, typically single operator). The 409 Conflict check provides a reasonable guard.

2. **CourseWaitlist lazy creation race** -- Two simultaneous POSTs for the same course-date could both try to create the CourseWaitlist. The unique index on `(CourseId, Date)` will cause one to fail with a DbUpdateException. The endpoint should handle this by catching the exception and retrying the lookup. Alternatively, use `FirstOrDefaultAsync` after the save failure. For v1, the retry pattern is acceptable.

3. **Time input format consistency** -- The HTML `<input type="time">` returns times in `HH:mm` format, which matches what the backend expects. However, the API also needs to handle `HH:mm:ss` format gracefully (some browsers may include seconds). The backend should parse with `TimeOnly.TryParseExact` using both `"HH:mm"` and `"HH:mm:ss"` formats.

4. **Event publisher registration** -- The `InProcessDomainEventPublisher` uses `IServiceProvider.GetServices()` to resolve handlers. Since no handlers are registered in this story, the publisher will resolve an empty list and do nothing. This is intentional. Future stories register handlers.

5. **Date navigation** -- The date picker defaults to today. The waitlist data is per-date. Changing the date triggers a new query. The date is passed in the POST body (not just the URL), so the form submission always uses the currently selected date.
