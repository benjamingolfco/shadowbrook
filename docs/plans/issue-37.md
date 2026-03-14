# Implementation Plan: Issue #37 -- Walk-Up Golfer Claims Tee Time Slot

**Issue:** #37 -- As a walk-up golfer, I can claim a tee time slot when notified
**Story Points:** 5
**Date:** 2026-03-13

---

## Overview

When an operator adds a tee time to the walk-up waitlist (via `POST /courses/{courseId}/walkup-waitlist/requests`), the existing `TeeTimeRequestAdded` domain event fires. This issue adds:

1. An event handler that matches eligible golfers to the request and sends SMS notifications with a claim link
2. A `WaitlistOffer` entity for token-based unauthenticated access
3. A `WaitlistRequestAcceptance` entity (per the waitlist architecture doc) to record who claimed the slot
4. Two new unauthenticated API endpoints: view an offer, accept an offer
5. A frontend offer claim page at `/book/walkup/:token`
6. A `WaitlistOfferAccepted` event + handler for booking creation and cleanup

**Owner note:** For v1, SMS is delivered via the existing `InMemoryTextMessageService` (in-app message visible at `/dev/sms`). The link in the SMS body points to the frontend offer page. No real SMS vendor needed.

---

## Implementation Sequence

1. Backend data model (entities, EF config, migration)
2. Domain events
3. Event handlers (notification on request added, booking on acceptance)
4. API endpoints (view offer, accept offer)
5. Frontend feature (offer claim page)
6. Tests (domain unit tests, integration tests, frontend tests)

---

## Phase 1: Backend Data Model

### 1.1 Create `WaitlistOffer` entity

**File:** `src/backend/Shadowbrook.Api/Models/WaitlistOffer.cs`

This is a lightweight infrastructure entity (not a domain aggregate) for SMS token tracking. It lives in `Models/` following the pattern of `Booking`, `Golfer`, and `GolferWaitlistEntry`.

```
WaitlistOffer
  Id                      Guid, PK (Guid.CreateVersion7())
  Token                   Guid, required, unique (Guid.CreateVersion7() -- separate from Id)
  TeeTimeRequestId        Guid, FK -> WaitlistRequests, required
  GolferWaitlistEntryId   Guid, FK -> GolferWaitlistEntries, required
  CourseId                Guid, FK -> Courses, required (denormalized for query convenience)
  CourseName              string, required (denormalized for display in the offer page)
  Date                    DateOnly, required (denormalized from CourseWaitlist)
  TeeTime                 TimeOnly, required (denormalized from TeeTimeRequest)
  GolfersNeeded           int, required (denormalized from TeeTimeRequest)
  GolferName              string, required (denormalized from GolferWaitlistEntry)
  GolferPhone             string, required (denormalized from GolferWaitlistEntry)
  Status                  string, required (enum: Pending, Accepted, Expired)
  ExpiresAt               DateTimeOffset, required
  CreatedAt               DateTimeOffset, required
```

**Why denormalize?** The offer view endpoint is unauthenticated and accessed via token only. Denormalizing avoids multi-table joins on a public endpoint. The offer is a snapshot of the state when it was created.

**Why separate Token from Id?** The `Token` is the credential exposed in the SMS URL. Keeping it separate from `Id` means internal references use `Id` and external references use `Token`. This is defense-in-depth -- if someone observes an `Id` in logs or API responses, they cannot use it to claim the offer.

### 1.2 Create `WaitlistRequestAcceptance` entity

**File:** `src/backend/Shadowbrook.Api/Models/WaitlistRequestAcceptance.cs`

Per the waitlist architecture doc (section 2.1). This is the junction between `TeeTimeRequest` (WaitlistRequest table) and `GolferWaitlistEntry`.

```
WaitlistRequestAcceptance
  Id                      Guid, PK (Guid.CreateVersion7())
  WaitlistRequestId       Guid, FK -> WaitlistRequests, required
  GolferWaitlistEntryId   Guid, FK -> GolferWaitlistEntries, required
  WaitlistOfferId         Guid, FK -> WaitlistOffers, required
  AcceptedAt              DateTimeOffset, required
  CreatedAt               DateTimeOffset, required

  Unique constraint: (WaitlistRequestId, GolferWaitlistEntryId)
```

### 1.3 EF Configuration for `WaitlistOffer`

**File:** `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`

```csharp
public class WaitlistOfferConfiguration : IEntityTypeConfiguration<WaitlistOffer>
{
    public void Configure(EntityTypeBuilder<WaitlistOffer> builder)
    {
        builder.ToTable("WaitlistOffers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Token).IsRequired();
        builder.HasIndex(o => o.Token).IsUnique();

        builder.Property(o => o.CourseName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.GolferName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.GolferPhone).IsRequired().HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(o => o.TeeTimeRequestId);
        builder.HasIndex(o => new { o.GolferWaitlistEntryId, o.TeeTimeRequestId });
    }
}
```

### 1.4 EF Configuration for `WaitlistRequestAcceptance`

**File:** `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistRequestAcceptanceConfiguration.cs`

```csharp
public class WaitlistRequestAcceptanceConfiguration : IEntityTypeConfiguration<WaitlistRequestAcceptance>
{
    public void Configure(EntityTypeBuilder<WaitlistRequestAcceptance> builder)
    {
        builder.ToTable("WaitlistRequestAcceptances");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasIndex(a => new { a.WaitlistRequestId, a.GolferWaitlistEntryId }).IsUnique();
        builder.HasIndex(a => a.WaitlistOfferId);
    }
}
```

### 1.5 Register entities in `ApplicationDbContext`

**File:** `src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs`

Add two new `DbSet<>` properties:

```csharp
public DbSet<WaitlistOffer> WaitlistOffers => Set<WaitlistOffer>();
public DbSet<WaitlistRequestAcceptance> WaitlistRequestAcceptances => Set<WaitlistRequestAcceptance>();
```

In `OnModelCreating`, apply the two new configurations:

```csharp
modelBuilder.ApplyConfiguration(new WaitlistOfferConfiguration());
modelBuilder.ApplyConfiguration(new WaitlistRequestAcceptanceConfiguration());
```

### 1.6 Add `OfferStatus` enum

**File:** `src/backend/Shadowbrook.Api/Models/OfferStatus.cs`

```csharp
namespace Shadowbrook.Api.Models;

public enum OfferStatus
{
    Pending,
    Accepted,
    Expired
}
```

This lives in `Models/` rather than `Domain/` because `WaitlistOffer` is an infrastructure entity.

### 1.7 Add domain method to `TeeTimeRequest` for acceptance tracking

**File:** `src/backend/Shadowbrook.Domain/WalkUpWaitlist/TeeTimeRequest.cs`

Add a method to transition status when all slots are filled:

```csharp
public void MarkFulfilled()
{
    Status = RequestStatus.Fulfilled;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

The `RequestStatus` enum already has `Fulfilled`. The caller (acceptance endpoint) will check the acceptance count against `GolfersNeeded` and call this when the threshold is met.

### 1.8 EF Migration

**Migration name:** `AddWaitlistOffersAndAcceptances`

Run: `dotnet ef migrations add AddWaitlistOffersAndAcceptances --project src/backend/Shadowbrook.Api`

This creates:
- `WaitlistOffers` table
- `WaitlistRequestAcceptances` table

---

## Phase 2: Domain Events

### 2.1 `WaitlistOfferAccepted` event

**File:** `src/backend/Shadowbrook.Domain/WalkUpWaitlist/Events/WaitlistOfferAccepted.cs`

```csharp
public record WaitlistOfferAccepted : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required Guid WaitlistOfferId { get; init; }
    public required Guid TeeTimeRequestId { get; init; }
    public required Guid GolferWaitlistEntryId { get; init; }
    public required Guid CourseId { get; init; }
    public required string CourseName { get; init; }
    public required DateOnly Date { get; init; }
    public required TimeOnly TeeTime { get; init; }
    public required string GolferName { get; init; }
    public required string GolferPhone { get; init; }
    public required int GolfersNeeded { get; init; }
    public required int AcceptanceCount { get; init; }
}
```

Follows the exact `record` pattern established by `TeeTimeRequestAdded` and `GolferJoinedWaitlist`.

---

## Phase 3: Event Handlers

### 3.1 `TeeTimeRequestAddedNotifyHandler` -- Send SMS offers to eligible golfers

**File:** `src/backend/Shadowbrook.Api/Infrastructure/Events/TeeTimeRequestAddedNotifyHandler.cs`

This handler subscribes to `TeeTimeRequestAdded` (which already fires when an operator creates a request via `WalkUpWaitlist.AddTeeTimeRequest()`).

**Logic:**

1. Query `GolferWaitlistEntries` where:
   - `CourseWaitlistId == event.WaitlistId`
   - `IsWalkUp == true`
   - `IsReady == true`
   - `RemovedAt == null`
   - Ordered by `JoinedAt ASC` (FIFO)
2. For each eligible golfer entry, create a `WaitlistOffer`:
   - Token = `Guid.CreateVersion7()`
   - Status = `Pending`
   - ExpiresAt = `DateTimeOffset.UtcNow.AddMinutes(15)` (hardcoded for v1; waitlist settings in future #175)
   - Denormalized fields from the event and golfer entry
3. Persist all offers via `db.WaitlistOffers.AddRange(...)` + `db.SaveChangesAsync()`
4. For each offer, send SMS via `ITextMessageService`:
   ```
   "{CourseName}: {TeeTime} tee time just opened! Claim your spot: {baseUrl}/book/walkup/{token} - You have 15 minutes."
   ```
5. The base URL comes from configuration (`IConfiguration["App:BaseUrl"]`). For dev, this defaults to `http://localhost:3000`.

**Important:** This handler runs within the `InProcessDomainEventPublisher` try/catch, so failures are logged but don't break the operator's request creation.

**Registration in `Program.cs`:**

```csharp
builder.Services.AddScoped<IDomainEventHandler<TeeTimeRequestAdded>, TeeTimeRequestAddedNotifyHandler>();
```

### 3.2 `WaitlistOfferAcceptedHandler` -- Create booking and clean up

**File:** `src/backend/Shadowbrook.Api/Infrastructure/Events/WaitlistOfferAcceptedHandler.cs`

Subscribes to `WaitlistOfferAccepted`.

**Logic:**

1. Create a `Booking`:
   - `CourseId` = event.CourseId
   - `Date` = event.Date
   - `Time` = event.TeeTime
   - `GolferName` = event.GolferName
   - `PlayerCount` = 1 (per planning spec: each acceptance = 1 player)
2. If `event.AcceptanceCount >= event.GolfersNeeded`:
   - Load the `TeeTimeRequest` by ID and call `MarkFulfilled()`
   - Expire all other pending `WaitlistOffer` rows for this `TeeTimeRequestId`
3. Soft-delete the golfer's waitlist entry (`RemovedAt = DateTimeOffset.UtcNow`)
4. Send confirmation SMS: `"You're booked! {CourseName} at {TeeTime} on {Date}. See you on the course!"`
5. `db.SaveChangesAsync()`

**Registration in `Program.cs`:**

```csharp
builder.Services.AddScoped<IDomainEventHandler<WaitlistOfferAccepted>, WaitlistOfferAcceptedHandler>();
```

---

## Phase 4: API Endpoints

### 4.1 `WaitlistOfferEndpoints`

**File:** `src/backend/Shadowbrook.Api/Endpoints/WaitlistOfferEndpoints.cs`

Extension method pattern: `MapWaitlistOfferEndpoints(this IEndpointRouteBuilder app)`

These endpoints are **unauthenticated** -- the token IS the credential. They do NOT go through `CourseExistsFilter` or tenant scoping. They should be mapped directly on `app` (not on the `api` group that adds validation filter), OR on their own group.

#### `GET /waitlist/offers/{token:guid}`

**Purpose:** View the offer details (called when golfer opens the SMS link).

**Logic:**

1. Query `WaitlistOffers` by `Token` (not by `Id`)
2. If not found, return `404 { error: "Offer not found." }`
3. If found, check expiration:
   - If `Status == Pending && ExpiresAt < DateTimeOffset.UtcNow`, update to `Expired`, save, and return the expired offer
   - Otherwise return as-is
4. Return `200` with:
   ```json
   {
     "token": "...",
     "courseName": "Pine Valley",
     "date": "2026-03-13",
     "teeTime": "09:20",
     "golfersNeeded": 2,
     "golferName": "Jane Smith",
     "status": "Pending",
     "expiresAt": "2026-03-13T14:35:00Z"
   }
   ```

**Response record:** `WaitlistOfferResponse`

#### `POST /waitlist/offers/{token:guid}/accept`

**Purpose:** Golfer claims the tee time.

**Logic:**

1. Query `WaitlistOffers` by `Token`
2. If not found, return `404`
3. If `Status != Pending`, return `409 { error: "This offer is no longer available." }`
4. If `ExpiresAt < DateTimeOffset.UtcNow`, update to `Expired`, save, return `409 { error: "This offer has expired." }`
5. Check concurrency: count existing `WaitlistRequestAcceptances` for this `TeeTimeRequestId`. If count >= `GolfersNeeded` (from the denormalized field on offer), return `409 { error: "All slots have been filled." }`
6. Create `WaitlistRequestAcceptance`:
   - `WaitlistRequestId` = offer.TeeTimeRequestId
   - `GolferWaitlistEntryId` = offer.GolferWaitlistEntryId
   - `WaitlistOfferId` = offer.Id
   - `AcceptedAt` = `DateTimeOffset.UtcNow`
7. Update offer `Status = Accepted`
8. Try `db.SaveChangesAsync()`. If `DbUpdateException` due to unique constraint violation on `(WaitlistRequestId, GolferWaitlistEntryId)`, return `409 { error: "You have already claimed this slot." }`
9. After save, raise `WaitlistOfferAccepted` domain event on the acceptance entity (or publish manually via `IDomainEventPublisher`)

**Note on event raising:** Since `WaitlistRequestAcceptance` is an infrastructure model (not a domain `Entity` subclass with `AddDomainEvent`), the endpoint should publish the event directly:

```csharp
await eventPublisher.PublishAsync(new WaitlistOfferAccepted { ... }, ct);
```

This is acceptable because the event publisher is already injected and used in `ApplicationDbContext.SaveChangesAsync()`. Direct publishing after save follows the same fire-and-forget pattern.

10. Return `200` with:
    ```json
    {
      "status": "Accepted",
      "courseName": "Pine Valley",
      "date": "2026-03-13",
      "teeTime": "09:20",
      "golferName": "Jane Smith",
      "message": "You're booked!"
    }
    ```

**Response record:** `WaitlistOfferAcceptResponse`

#### Endpoint registration

**In `Program.cs`**, register OUTSIDE the `api` group (no tenant middleware needed):

```csharp
app.MapWaitlistOfferEndpoints();
```

The endpoints themselves can still use `.AddValidationFilter()` if needed, but since there are no request bodies requiring validation on the GET and the POST has no body, it is not necessary.

### 4.2 Request/Response records

All inline in `WaitlistOfferEndpoints.cs` following established pattern:

```csharp
public record WaitlistOfferResponse(
    Guid Token,
    string CourseName,
    string Date,
    string TeeTime,
    int GolfersNeeded,
    string GolferName,
    string Status,
    DateTimeOffset ExpiresAt);

public record WaitlistOfferAcceptResponse(
    string Status,
    string CourseName,
    string Date,
    string TeeTime,
    string GolferName,
    string Message);
```

---

## Phase 5: Frontend

### 5.1 Feature folder structure

```
src/web/src/features/walk-up/
  index.tsx                          -- Route definition
  pages/
    WalkUpOfferPage.tsx              -- Main page component
  components/
    OfferCard.tsx                    -- Offer details card
    CountdownTimer.tsx              -- Countdown to expiration
    AcceptConfirmation.tsx          -- Success state after accepting
  hooks/
    useWalkUpOffer.ts               -- TanStack Query hooks
  __tests__/
    WalkUpOfferPage.test.tsx        -- Page integration tests
    CountdownTimer.test.tsx         -- Timer unit tests
```

### 5.2 Types

**File:** `src/web/src/types/waitlist.ts` (add to existing file)

```typescript
// Walk-up offer (golfer claim flow)
export interface WaitlistOfferResponse {
  token: string;
  courseName: string;
  date: string;       // "yyyy-MM-dd"
  teeTime: string;    // "HH:mm"
  golfersNeeded: number;
  golferName: string;
  status: 'Pending' | 'Accepted' | 'Expired';
  expiresAt: string;  // ISO 8601
}

export interface WaitlistOfferAcceptResponse {
  status: string;
  courseName: string;
  date: string;
  teeTime: string;
  golferName: string;
  message: string;
}
```

### 5.3 Query keys

**File:** `src/web/src/lib/query-keys.ts` (add to existing)

```typescript
walkUpOffer: {
  byToken: (token: string) => ['walk-up-offer', token] as const,
},
```

### 5.4 Hooks

**File:** `src/web/src/features/walk-up/hooks/useWalkUpOffer.ts`

```typescript
import { useQuery, useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { WaitlistOfferResponse, WaitlistOfferAcceptResponse } from '@/types/waitlist';

export function useWalkUpOffer(token: string) {
  return useQuery({
    queryKey: queryKeys.walkUpOffer.byToken(token),
    queryFn: () => api.get<WaitlistOfferResponse>(`/waitlist/offers/${token}`),
    enabled: !!token,
    refetchOnWindowFocus: false,  // Don't refetch while golfer is deciding
  });
}

export function useAcceptOffer(token: string) {
  return useMutation({
    mutationFn: () => api.post<WaitlistOfferAcceptResponse>(`/waitlist/offers/${token}/accept`, {}),
  });
}
```

### 5.5 Route registration

**File:** `src/web/src/features/walk-up/index.tsx`

```typescript
import { Routes, Route } from 'react-router';
import WalkUpOfferPage from './pages/WalkUpOfferPage';

export default function WalkUpFeature() {
  return (
    <Routes>
      <Route path=":token" element={<WalkUpOfferPage />} />
    </Routes>
  );
}
```

**File:** `src/web/src/app/router.tsx` (add new route)

```typescript
const WalkUpOfferFeature = lazy(() => import('@/features/walk-up'));

// Add to routes array:
{
  path: '/book/walkup/*',
  element: (
    <LazyFeature><WalkUpOfferFeature /></LazyFeature>
  ),
},
```

This route is unauthenticated -- no `AuthGuard` or `RoleGuard` wrapper, same pattern as `/join/*`.

### 5.6 `WalkUpOfferPage`

**File:** `src/web/src/features/walk-up/pages/WalkUpOfferPage.tsx`

**States and transitions:**

1. **Loading** -- `useWalkUpOffer` is pending. Show skeleton layout.
2. **Active Offer** -- Status is `Pending` and not expired. Show offer card with countdown and "Claim This Tee Time" button.
3. **Expired** -- Status is `Expired` or countdown reaches zero. In-place transition: disable button, show "This offer has expired" message. When countdown reaches zero client-side, refetch to confirm server state.
4. **Not Found** -- 404 from API. Show "Offer not found" message.
5. **Accept Error** -- Mutation failed. Show inline error message below button, button remains enabled for retry.
6. **Success** -- Mutation succeeded. Show checkmark animation + "You're booked!" confirmation.
7. **Generic Error** -- Non-404 API error. Show retry button.

**Layout:** Mobile-first standalone page. No layout wrapper (no sidebar, no nav). Centered vertically like `WalkupJoinPage`.

```
<div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
  <div className="w-full max-w-sm">
    <h1 className="text-xl font-bold text-center mb-8">Shadowbrook</h1>
    {/* state-dependent content */}
  </div>
</div>
```

**Accept flow:** When golfer taps "Claim This Tee Time", show an `AlertDialog` (shadcn) for confirmation:
- Title: "Claim this tee time?"
- Description: "{CourseName} at {TeeTime} on {Date}"
- Cancel button + Confirm button
- On confirm: call `acceptOffer.mutate()`

### 5.7 `OfferCard`

**File:** `src/web/src/features/walk-up/components/OfferCard.tsx`

Uses shadcn `Card`, `Separator`, `Button`.

Props: `offer: WaitlistOfferResponse`, `onAccept: () => void`, `isAccepting: boolean`, `acceptError: string | null`

Content:
- Course name (h2)
- Date formatted (e.g., "Thursday, March 13")
- Tee time formatted (e.g., "9:20 AM")
- Spots available: `{golfersNeeded}`
- `CountdownTimer` component
- `Separator`
- "Hi, {firstName}!" greeting
- "Claim This Tee Time" button (lg size, full width)
- Error message below button if `acceptError` is set

### 5.8 `CountdownTimer`

**File:** `src/web/src/features/walk-up/components/CountdownTimer.tsx`

Props: `expiresAt: string`, `onExpired: () => void`

Uses `useState` + `useEffect` with a 1-second `setInterval`.

- Calculates remaining seconds from `expiresAt` ISO string
- Displays as "MM:SS remaining" or "X minutes remaining" (switch to MM:SS when under 5 minutes)
- `role="timer"` and `aria-live="polite"` on the container (update `aria-live` content less frequently than visual -- every 30s -- to avoid screen reader fatigue)
- When countdown reaches 0, calls `onExpired()` callback
- Returns `null` or an "Expired" badge when time is up

### 5.9 `AcceptConfirmation`

**File:** `src/web/src/features/walk-up/components/AcceptConfirmation.tsx`

Props: `response: WaitlistOfferAcceptResponse`

Reuses the checkmark pattern from `features/walkup/components/Confirmation.tsx`:
- Green checkmark circle
- "You're booked!" heading
- Course name, date, time details
- "See you on the course!" message

### 5.10 shadcn components

All needed components already exist:
- `card.tsx` -- Card, CardHeader, CardContent, etc.
- `separator.tsx` -- Separator
- `button.tsx` -- Button
- `alert-dialog.tsx` -- AlertDialog, AlertDialogTrigger, etc.
- `skeleton.tsx` -- Skeleton

No new shadcn components need to be installed.

---

## Phase 6: Program.cs Changes

**File:** `src/backend/Shadowbrook.Api/Program.cs`

1. Register event handlers:
   ```csharp
   builder.Services.AddScoped<IDomainEventHandler<TeeTimeRequestAdded>, TeeTimeRequestAddedNotifyHandler>();
   builder.Services.AddScoped<IDomainEventHandler<WaitlistOfferAccepted>, WaitlistOfferAcceptedHandler>();
   ```

2. Add app base URL configuration:
   ```csharp
   // In appsettings.Development.json:
   // "App": { "BaseUrl": "http://localhost:3000" }
   ```

3. Map offer endpoints (OUTSIDE the `api` group, no tenant scoping):
   ```csharp
   app.MapWaitlistOfferEndpoints();
   ```

4. Add `WaitlistOfferAccepted`-related domain exception handling if needed (unlikely -- the endpoint uses Results directly, not domain exceptions).

---

## Phase 7: Tests

### 7.1 Domain Unit Tests

**File:** `tests/Shadowbrook.Domain.Tests/WalkUpWaitlist/TeeTimeRequestTests.cs` (modify existing)

Add test:
- `MarkFulfilled_SetsStatusToFulfilled`

### 7.2 API Integration Tests

**File:** `tests/Shadowbrook.Api.Tests/WaitlistOfferEndpointsTests.cs` (new)

Test scenarios (each test sets up a course, opens waitlist, adds golfer, creates request -- this triggers SMS offers):

**GET /waitlist/offers/{token}:**

1. `ViewOffer_ValidToken_Returns200WithOfferDetails` -- Verify all fields present
2. `ViewOffer_InvalidToken_Returns404`
3. `ViewOffer_ExpiredOffer_ReturnsExpiredStatus` -- Create offer, manipulate ExpiresAt in DB to past, verify status is Expired

**POST /waitlist/offers/{token}/accept:**

4. `AcceptOffer_ValidPendingOffer_Returns200` -- Happy path: verify response contains booking confirmation
5. `AcceptOffer_CreatesBooking` -- After accept, verify a Booking exists in DB with correct CourseId, Date, Time, GolferName, PlayerCount=1
6. `AcceptOffer_RemovesGolferFromWaitlist` -- After accept, verify GolferWaitlistEntry has RemovedAt set
7. `AcceptOffer_SendsConfirmationSms` -- Check InMemoryTextMessageService for confirmation message
8. `AcceptOffer_ExpiredOffer_Returns409`
9. `AcceptOffer_AlreadyAcceptedOffer_Returns409`
10. `AcceptOffer_InvalidToken_Returns404`
11. `AcceptOffer_AllSlotsFilled_Returns409` -- Create request with GolfersNeeded=1, accept it, then try second acceptance with different golfer

**SMS notification (triggered by TeeTimeRequestAdded):**

12. `CreateRequest_WithEligibleGolfers_SendsOfferSms` -- Open waitlist, add golfer, create request, verify SMS sent via InMemoryTextMessageService
13. `CreateRequest_NoEligibleGolfers_NoSmsSent` -- Open waitlist, create request (no golfers), verify no SMS
14. `CreateRequest_WithEligibleGolfers_CreatesWaitlistOffers` -- Verify WaitlistOffer rows created in DB

**Helper methods:** The test class needs helpers to:
- Create test tenant + course (reuse from `WalkUpWaitlistEndpointsTests`)
- Open waitlist
- Add golfer to waitlist
- Create tee time request
- Extract offer token from SMS messages (read from `/dev/sms` endpoint, parse token from URL in message body)

**Accessing InMemoryTextMessageService in tests:** The `TestWebApplicationFactory` uses the same `InMemoryTextMessageService` singleton. Tests can read sent messages via `GET /dev/sms` or by resolving the service from the factory's service provider.

### 7.3 Frontend Tests

**File:** `src/web/src/features/walk-up/__tests__/WalkUpOfferPage.test.tsx`

Mock `useWalkUpOffer` and `useAcceptOffer` hooks.

Test scenarios:
1. Renders loading skeleton when query is pending
2. Renders offer card with correct details for active offer
3. Shows countdown timer
4. Shows "Claim This Tee Time" button
5. Shows AlertDialog on button click
6. Transitions to success state after acceptance
7. Shows expired state when status is Expired
8. Shows not found state on 404 error
9. Shows error message on accept failure

**File:** `src/web/src/features/walk-up/__tests__/CountdownTimer.test.tsx`

1. Displays remaining time correctly
2. Calls onExpired when countdown reaches zero (use `vi.useFakeTimers()`)
3. Has correct ARIA attributes

---

## Key Design Decisions

### Token-based unauthenticated access

The SMS contains a URL with a GUID token. This token is the credential. No login required. The `WaitlistOffer.Token` field is a separate GUID from the `Id` to prevent ID enumeration. The offer endpoints do not require tenant headers or auth middleware.

### Walk-up broadcast model (not FIFO cascade)

For the walk-up scenario, ALL eligible golfers on the waitlist receive SMS simultaneously. This is "first to tap wins." This differs from the future remote waitlist (#3) which will use FIFO cascading with 15-minute individual windows. The 15-minute window in the walk-up scenario is a "you have 15 minutes before the offer disappears" fairness timeout, not a per-golfer cascade.

### Concurrency handling on acceptance

The unique constraint on `(WaitlistRequestId, GolferWaitlistEntryId)` prevents double-acceptance by the same golfer. The count check (`acceptanceCount < golfersNeeded`) before creating the acceptance provides optimistic concurrency for the "all slots filled" case. In the rare race condition where two golfers accept simultaneously for the last slot, one will succeed and the other will get a 409 after the count check on the re-read.

### Event-driven side effects

The `WaitlistOfferAccepted` event handler creates the booking and sends confirmation SMS. This follows the project principle of event-driven backend. The handler failure is isolated -- if booking creation fails, the acceptance record still exists and can be reconciled. In practice, with in-process events and a single DB transaction, this is effectively atomic.

### In-app SMS for v1

Per the owner's note, SMS goes through `InMemoryTextMessageService`. The SMS body contains a clickable URL to the frontend offer page. Developers can see all SMS at `GET /dev/sms` or browse the conversation for a specific phone at `GET /dev/sms/conversations/{phone}`. No Twilio integration needed for v1.

---

## Configuration

**File:** `src/backend/Shadowbrook.Api/appsettings.Development.json`

Add:
```json
{
  "App": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

This is used by `TeeTimeRequestAddedNotifyHandler` to construct the claim URL in SMS messages.

---

## Risks

1. **Offer expiration is lazy** -- Offers are checked for expiration on read (GET endpoint) and on accept (POST endpoint). There is no background job that expires them. A golfer who never opens the link will have their offer sit as `Pending` in the DB. This is acceptable for v1; the operator can see request status and manage accordingly.

2. **No retry on SMS failure** -- If `ITextMessageService.SendAsync` throws, the exception is caught by the `InProcessDomainEventPublisher` and logged. The offer record is created but the SMS is not sent. For v1 with in-memory SMS, this cannot happen. For production with Twilio, this would need retry logic.

3. **SQLite filtered index limitation** -- The `GolferWaitlistEntryConfiguration` uses `HasFilter("[RemovedAt] IS NULL")` which is SQL Server syntax. The test suite uses SQLite which ignores this. The `WaitlistOfferConfiguration` and `WaitlistRequestAcceptanceConfiguration` use simple indexes without filters, avoiding this issue.

4. **Clock skew** -- The expiration check compares `DateTimeOffset.UtcNow` on the server. If the client clock differs from the server, the countdown timer may show different remaining time than the server enforces. The server is authoritative; the client timer is cosmetic.
