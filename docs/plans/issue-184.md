# Implementation Plan: Issue #184 - Walk-up Waitlist SMS Offer

**Issue:** #184 - As a walk-up golfer, I receive a text offer when a tee time opens
**Story Points:** 8
**Branch:** `issue/184-walkup-sms-offer`
**Date:** 2026-03-11

---

## 1. Approach

When an operator creates a `TeeTimeRequest` (via `POST /courses/{courseId}/walkup-waitlist/requests`), the existing `TeeTimeRequestAdded` domain event fires. A new event handler subscribes to this event, queries the walk-up queue for the first eligible golfer, creates a `WaitlistOffer` record, and sends an SMS via `ITextMessageService`. A new inbound SMS webhook endpoint receives Y/N replies, looks up the active offer by phone number, and processes the claim or decline.

This story introduces three new things: (1) a `WaitlistOffer` entity to track offer state, (2) a `TeeTimeRequestAddedOfferHandler` that sends the initial SMS, and (3) a `POST /webhooks/sms/inbound` endpoint for processing replies. It also introduces a `WaitlistRequestAcceptance` entity (from the architecture plan) to record successful claims.

**No frontend changes are required** - this is entirely SMS-driven from the golfer's perspective, and the operator already has the tee sheet and waitlist UI.

---

## 2. Data Model

### 2.1 New Entity: WaitlistOffer

Tracks an individual SMS offer sent to a golfer for a specific tee time request.

```
WaitlistOffer
  Id                      Guid, PK (UUIDv7)
  TeeTimeRequestId        Guid, FK -> TeeTimeRequests (WaitlistRequests table), required
  GolferWaitlistEntryId   Guid, FK -> GolferWaitlistEntries, required
  GolferPhone             string, required (denormalized E.164 - for inbound lookup)
  CourseName              string, required (denormalized - for SMS message text)
  TeeTime                 TimeOnly, required (denormalized - for SMS message text)
  OfferDate               DateOnly, required (denormalized - for SMS message text)
  Status                  string, required (Pending, Accepted, Declined, Expired)
  ResponseWindowMinutes   int, required (default: 5)
  OfferedAt               DateTimeOffset, required
  ExpiresAt               DateTimeOffset, required
  RespondedAt             DateTimeOffset, nullable
  CreatedAt               DateTimeOffset, required

  Index: (GolferPhone, Status) -- for inbound SMS lookup of active offers
  Index: (TeeTimeRequestId)    -- for checking existing offers per request
```

**Design decisions:**
- `GolferPhone` is denormalized to allow O(1) lookup on inbound SMS without joining through GolferWaitlistEntry -> Golfer. Inbound SMS only provides a phone number.
- `CourseName`, `TeeTime`, `OfferDate` are denormalized for confirmation SMS messages.
- `ResponseWindowMinutes` is stored per-offer for future configurability (hardcoded to 5 for now; #176 will make this operator-configurable).
- `ExpiresAt = OfferedAt + ResponseWindowMinutes` computed at creation time.
- Status enum: `Pending` (sent, awaiting reply), `Accepted` (golfer replied Y), `Declined` (golfer replied N), `Expired` (window passed - handled by follow-up story).

### 2.2 New Entity: WaitlistRequestAcceptance

From the architecture plan (Section 2.1). Records which golfer accepted a request.

```
WaitlistRequestAcceptance
  Id                      Guid, PK (UUIDv7)
  WaitlistRequestId       Guid, FK -> TeeTimeRequests (WaitlistRequests table), required
  GolferWaitlistEntryId   Guid, FK -> GolferWaitlistEntries, required
  AcceptedAt              DateTimeOffset, required
  CreatedAt               DateTimeOffset, required

  Unique constraint: (WaitlistRequestId) -- only one acceptance per request in this story's scope
```

**Note:** The architecture plan shows `UNIQUE(WaitlistRequestId, GolferWaitlistEntryId)` but for this story, we enforce `UNIQUE(WaitlistRequestId)` since only one golfer can claim a slot. The follow-up story for multi-golfer requests can relax this if needed.

---

## 3. File-by-File Breakdown

### 3.1 New Files

#### Domain Layer

**`src/backend/Shadowbrook.Domain/WalkUpWaitlist/OfferStatus.cs`**
- Enum: `Pending`, `Accepted`, `Declined`, `Expired`

**`src/backend/Shadowbrook.Domain/WalkUpWaitlist/Events/WaitlistOfferAccepted.cs`**
- Domain event record implementing `IDomainEvent`
- Fields: `EventId`, `OccurredAt`, `WaitlistOfferId`, `TeeTimeRequestId`, `GolferWaitlistEntryId`, `GolferPhone`, `CourseName`, `TeeTime`, `OfferDate`
- Published when a golfer replies Y and the offer is accepted

**`src/backend/Shadowbrook.Domain/WalkUpWaitlist/Events/WaitlistOfferDeclined.cs`**
- Domain event record implementing `IDomainEvent`
- Fields: `EventId`, `OccurredAt`, `WaitlistOfferId`, `TeeTimeRequestId`, `GolferWaitlistEntryId`, `GolferPhone`
- Published when a golfer replies N

#### API / Infrastructure Layer

**`src/backend/Shadowbrook.Api/Models/WaitlistOffer.cs`**
- EF model class extending `Entity` (to support domain events)
- Properties matching the data model above
- Method: `Accept(DateTimeOffset respondedAt)` - sets Status=Accepted, RespondedAt; raises `WaitlistOfferAccepted` event
- Method: `Decline(DateTimeOffset respondedAt)` - sets Status=Declined, RespondedAt; raises `WaitlistOfferDeclined` event
- Guard: both methods throw if Status is not Pending

**`src/backend/Shadowbrook.Api/Models/WaitlistRequestAcceptance.cs`**
- Simple EF model class (no domain events needed)
- Properties: `Id`, `WaitlistRequestId`, `GolferWaitlistEntryId`, `AcceptedAt`, `CreatedAt`

**`src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`**
- Table name: `WaitlistOffers`
- Configure FK relationships, indexes, string conversion for Status enum
- Index on `(GolferPhone, Status)` with filter `[Status] = 'Pending'` for efficient inbound SMS lookup

**`src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistRequestAcceptanceConfiguration.cs`**
- Table name: `WaitlistRequestAcceptances`
- Configure FK relationships
- Unique index on `WaitlistRequestId` (one acceptance per request for now)

**`src/backend/Shadowbrook.Api/Infrastructure/Events/TeeTimeRequestAddedOfferHandler.cs`**
- Implements `IDomainEventHandler<TeeTimeRequestAdded>`
- Injected dependencies: `ApplicationDbContext`, `ITextMessageService`, `ILogger`
- Handler logic:
  1. Query `GolferWaitlistEntries` for the waitlist where `RemovedAt IS NULL` and `IsReady = true`, ordered by `JoinedAt ASC`
  2. If queue is empty, log and return (AC4: no SMS sent)
  3. Take the first entry (FIFO)
  4. Look up course name from `Courses` table via the waitlist's `CourseId`
  5. Look up the tee time from the `TeeTimeRequest` entity
  6. Create `WaitlistOffer` record with `Status = Pending`, `OfferedAt = now`, `ExpiresAt = now + 5 minutes`
  7. Save to DB
  8. Send SMS: `"[CourseName]: A {TeeTime} tee time just opened! Reply Y to claim or N to pass. You have {window} minutes."`
  9. Log the offer

**`src/backend/Shadowbrook.Api/Endpoints/SmsWebhookEndpoints.cs`**
- Extension method: `MapSmsWebhookEndpoints(this IEndpointRouteBuilder app)`
- Single endpoint: `POST /webhooks/sms/inbound`
- Request model: `InboundSmsWebhookRequest` record with `From` (phone number) and `Body` (message text)
  - Note: For dev, this accepts JSON. For production Twilio, this would be form-encoded. The dev `InMemoryTextMessageService` + `DevSmsEndpoints` already handle dev-mode simulation. This endpoint is the "real" processor.
- Endpoint logic:
  1. Normalize `From` phone number via `PhoneNormalizer`
  2. Query `WaitlistOffers` where `GolferPhone == normalizedPhone AND Status == "Pending"`, ordered by `OfferedAt DESC`, take first
  3. If no active offer found, return 200 OK with no action (graceful - don't error on random SMS)
  4. Parse `Body`: trim, uppercase, check first character
     - "Y" -> claim flow
     - "N" -> decline flow
     - Anything else -> send help SMS: "Reply Y to claim or N to pass.", return 200
  5. **Claim flow (Y):**
     - Check if offer has expired (`ExpiresAt < now`). If expired, send SMS "Sorry, the response window has closed." Return 200. (Note: status remains Pending - the follow-up story handles expiration cascading)
     - Check if `WaitlistRequestAcceptance` already exists for this `TeeTimeRequestId`. If so, send SMS "Sorry, this slot has already been claimed." Return 200.
     - Call `offer.Accept(now)` (sets status, raises event)
     - Create `WaitlistRequestAcceptance` record
     - Create `Booking` record matching existing pattern: `CourseId` from the waitlist, `Date` from the waitlist date, `Time` from the offer's `TeeTime`, `GolferName` from `GolferWaitlistEntry.GolferName`, `PlayerCount` from `TeeTimeRequest.GolfersNeeded`
     - Update `TeeTimeRequest.Status` to `Fulfilled` (requires adding a method to domain entity)
     - Soft-delete the `GolferWaitlistEntry` (set `RemovedAt = now`)
     - Save all changes (triggers domain events)
     - Send confirmation SMS: "Confirmed! You're booked for {TeeTime} at {CourseName}. See you on the first tee!"
  6. **Decline flow (N):**
     - Call `offer.Decline(now)` (sets status, raises event)
     - Soft-delete the `GolferWaitlistEntry` (set `RemovedAt = now`) - AC3: removed from queue
     - Save changes
     - Send confirmation SMS: "Got it. You've been removed from the waitlist at {CourseName}."
  7. Return 200 OK for all paths (webhook endpoints should not return errors to the SMS provider)
- Validator: `InboundSmsWebhookRequestValidator` - `From` must be a valid phone, `Body` must not be empty

#### Tests

**`tests/Shadowbrook.Api.Tests/SmsOfferIntegrationTests.cs`**
- Integration tests using `TestWebApplicationFactory`
- Test scenarios (see Section 5)

**`tests/Shadowbrook.Domain.Tests/WalkUpWaitlist/WaitlistOfferTests.cs`**
- Unit tests for `WaitlistOffer.Accept()` and `WaitlistOffer.Decline()` domain behavior

### 3.2 Modified Files

**`src/backend/Shadowbrook.Api/Infrastructure/Data/ApplicationDbContext.cs`**
- Add `DbSet<WaitlistOffer> WaitlistOffers`
- Add `DbSet<WaitlistRequestAcceptance> WaitlistRequestAcceptances`
- Apply new entity type configurations in `OnModelCreating`

**`src/backend/Shadowbrook.Api/Program.cs`**
- Register the new event handler: `builder.Services.AddScoped<IDomainEventHandler<TeeTimeRequestAdded>, TeeTimeRequestAddedOfferHandler>()`
- Map the webhook endpoints: `app.MapSmsWebhookEndpoints()` (outside the `api` group with validation filter, since webhooks have different auth/validation needs)

**`src/backend/Shadowbrook.Domain/WalkUpWaitlist/TeeTimeRequest.cs`**
- Add method `Fulfill()` that sets `Status = RequestStatus.Fulfilled` with guard (must be Pending)
- This keeps domain logic in the domain entity rather than mutating status directly from the endpoint

**`src/backend/Shadowbrook.Domain/WalkUpWaitlist/RequestStatus.cs`**
- Rename `Fulfilled` value if needed (it already exists in the enum, so no change needed)
- Verify the existing `Fulfilled` status matches the architecture plan's "Filled" concept

### 3.3 Migration

**`dotnet ef migrations add AddWaitlistOffersAndAcceptances --project src/backend/Shadowbrook.Api`**

Creates:
- `WaitlistOffers` table with all columns, FKs, and indexes
- `WaitlistRequestAcceptances` table with all columns, FKs, and unique index

---

## 4. Endpoint Design

### POST /webhooks/sms/inbound

This endpoint sits **outside** the normal tenant-scoped API group. It has no `ICurrentUser` context and no `CourseExistsFilter`. It is a public-facing webhook.

```
POST /webhooks/sms/inbound
Content-Type: application/json

{
  "From": "+15558675309",
  "Body": "Y"
}

Response: 200 OK (always, for all paths)
```

**Registration in Program.cs:**
```
// After the tenant-scoped api group
app.MapSmsWebhookEndpoints();  // no validation filter, no tenant middleware
```

The endpoint group should be:
```csharp
var group = app.MapGroup("/webhooks/sms");
group.MapPost("/inbound", HandleInboundSms);
```

**Important:** In dev mode, the existing `POST /dev/sms/inbound` endpoint on `DevSmsEndpoints` simulates receiving an inbound SMS by adding it to the `InMemoryTextMessageService` message store. The new `/webhooks/sms/inbound` endpoint is the **processing** endpoint that actually acts on the message. For development testing, a tester would:
1. Call `POST /webhooks/sms/inbound` with the simulated reply (this processes the Y/N)
2. Check `GET /dev/sms/conversations/{phone}` to see the full conversation thread

The existing `POST /dev/sms/inbound` should also be updated to call the webhook processing endpoint, OR the dev flow documentation should make clear that `/webhooks/sms/inbound` is the endpoint to use for testing replies. Recommend keeping them separate for clarity - `/dev/sms/inbound` is for inspecting the message log, `/webhooks/sms/inbound` is for processing.

---

## 5. Testing Strategy

### 5.1 Domain Unit Tests (`WaitlistOfferTests.cs`)

| Test | Description |
|------|-------------|
| `Accept_WhenPending_SetsAcceptedStatus` | Verify status transition and RespondedAt |
| `Accept_WhenNotPending_ThrowsDomainException` | Verify guard on double-accept |
| `Decline_WhenPending_SetsDeclinedStatus` | Verify status transition and RespondedAt |
| `Decline_WhenNotPending_ThrowsDomainException` | Verify guard on double-decline |
| `Accept_RaisesWaitlistOfferAccepted_Event` | Verify domain event is added |
| `Decline_RaisesWaitlistOfferDeclined_Event` | Verify domain event is added |

### 5.2 Domain Unit Tests (`TeeTimeRequestTests.cs` - modify existing)

| Test | Description |
|------|-------------|
| `Fulfill_WhenPending_SetsFulfilledStatus` | Verify status transition |
| `Fulfill_WhenNotPending_ThrowsDomainException` | Verify guard |

### 5.3 Integration Tests (`SmsOfferIntegrationTests.cs`)

Setup helper for each test:
1. Create tenant + course
2. Open waitlist
3. Add golfer to waitlist (phone: "+15558675309")
4. Create tee time request (which triggers the offer handler)

| Test | Scenario | Assertions |
|------|----------|------------|
| `CreateRequest_WithGolferInQueue_SendsSmsOffer` | AC1 | Check `GET /dev/sms/conversations/+15558675309` shows outbound SMS with course name, tee time, and window info |
| `CreateRequest_EmptyQueue_NoSmsSent` | AC4 | Check `GET /dev/sms` has no new messages after creating request |
| `InboundSms_ReplyY_ClaimsSlot` | AC2 | POST webhook with "Y", verify: confirmation SMS sent, booking created (check tee sheet), golfer removed from waitlist |
| `InboundSms_ReplyN_DeclinesAndRemoves` | AC3 | POST webhook with "N", verify: removal confirmation SMS sent, golfer removed from waitlist entries |
| `InboundSms_ReplyY_AfterExpiry_RejectsGracefully` | Edge case | Create offer, wait/manipulate time, reply Y, verify rejection SMS |
| `InboundSms_ReplyY_AlreadyClaimed_RejectsGracefully` | Edge case | Two golfers get offers (manually), first claims, second tries to claim |
| `InboundSms_UnknownPhone_Returns200` | Edge case | Random phone replies, no error |
| `InboundSms_GarbageBody_SendsHelpSms` | Edge case | Reply "HELLO", verify help message SMS sent |
| `InboundSms_ReplyY_CreatesBooking_VisibleOnTeeSheet` | Integration | Full flow: claim -> check `GET /tee-sheets?courseId=...&date=...` shows booked slot |
| `InboundSms_ReplyN_GolferNotInTodayEndpoint` | Integration | Decline -> check `GET /courses/{id}/walkup-waitlist/today` entries no longer includes golfer |

### 5.4 Time Handling for Expiry Tests

The offer expiry check (`ExpiresAt < now`) is time-dependent. Two approaches:

**Option A (Recommended):** Introduce a simple `ITimeProvider` interface (or use .NET 8+'s `TimeProvider` abstract class) injected into the webhook endpoint. In tests, use a fake that returns controlled timestamps. This is clean but adds a new abstraction.

**Option B:** Create offers with very short windows (1 second) in tests and use `Task.Delay`. Fragile but simpler.

**Recommendation:** Use .NET's built-in `TimeProvider` (available since .NET 8). Register `TimeProvider.System` in production DI, inject `FakeTimeProvider` in tests. The offer handler and webhook endpoint both accept `TimeProvider` for `UtcNow`.

---

## 6. SMS Message Templates

### Offer SMS (AC1)
```
{CourseName}: A {TeeTime} tee time just opened for {Date}! Reply Y to claim or N to pass. You have {WindowMinutes} min to respond.
```
Example: `Shadowbrook GC: A 9:20 AM tee time just opened for today! Reply Y to claim or N to pass. You have 5 min to respond.`

### Claim Confirmation SMS (AC2)
```
Confirmed! You're booked for {TeeTime} at {CourseName}. See you on the first tee!
```

### Decline Confirmation SMS (AC3)
```
Got it - you've been removed from the waitlist at {CourseName}.
```

### Expiry Rejection SMS
```
Sorry, the response window for the {TeeTime} tee time has closed.
```

### Already Claimed SMS
```
Sorry, the {TeeTime} slot at {CourseName} has already been taken.
```

### Help SMS (unrecognized reply)
```
Reply Y to claim the tee time or N to pass.
```

---

## 7. Sequence Diagrams

### Offer Flow (AC1 + AC4)

```
Operator -> POST /requests -> WalkUpWaitlistEndpoints
  -> Creates TeeTimeRequest
  -> SaveChangesAsync() commits + dispatches TeeTimeRequestAdded event
  -> TeeTimeRequestAddedOfferHandler.HandleAsync()
     -> Query GolferWaitlistEntries (first by JoinedAt, IsReady=true, RemovedAt=null)
     -> IF empty queue: log, return (AC4)
     -> Create WaitlistOffer (Pending)
     -> SaveChangesAsync()
     -> ITextMessageService.SendAsync(phone, offerMessage)
```

### Claim Flow (AC2)

```
Golfer SMS "Y" -> POST /webhooks/sms/inbound -> SmsWebhookEndpoints
  -> Normalize phone
  -> Query WaitlistOffers (GolferPhone=phone, Status=Pending)
  -> Check not expired, not already claimed
  -> offer.Accept(now)
  -> Create WaitlistRequestAcceptance
  -> Create Booking
  -> teeTimeRequest.Fulfill()
  -> Set GolferWaitlistEntry.RemovedAt
  -> SaveChangesAsync()
  -> ITextMessageService.SendAsync(phone, confirmationMessage)
  -> Return 200
```

### Decline Flow (AC3)

```
Golfer SMS "N" -> POST /webhooks/sms/inbound -> SmsWebhookEndpoints
  -> Normalize phone
  -> Query WaitlistOffers (GolferPhone=phone, Status=Pending)
  -> offer.Decline(now)
  -> Set GolferWaitlistEntry.RemovedAt
  -> SaveChangesAsync()
  -> ITextMessageService.SendAsync(phone, removalMessage)
  -> Return 200
```

---

## 8. Implementation Order

The recommended implementation sequence:

1. **Domain enums and events** - `OfferStatus`, `WaitlistOfferAccepted`, `WaitlistOfferDeclined`
2. **Domain method** - `TeeTimeRequest.Fulfill()`
3. **EF models** - `WaitlistOffer`, `WaitlistRequestAcceptance`
4. **EF configurations** - both new configuration classes
5. **DbContext changes** - add DbSets and apply configurations
6. **Migration** - `AddWaitlistOffersAndAcceptances`
7. **Event handler** - `TeeTimeRequestAddedOfferHandler`
8. **Webhook endpoint** - `SmsWebhookEndpoints`
9. **Program.cs registration** - handler DI + endpoint mapping
10. **Domain unit tests** - `WaitlistOfferTests`, `TeeTimeRequestTests` additions
11. **Integration tests** - `SmsOfferIntegrationTests`

---

## 9. Risks and Edge Cases

### 9.1 Phone Number Ambiguity

If a golfer has active offers from multiple courses (joined waitlists at two courses, both had tee times open simultaneously), the inbound SMS lookup will find multiple pending offers. The handler should pick the **most recent** offer (`OfferedAt DESC`). This is acceptable for v1 since walk-up golfers are physically at one course. The follow-up story for remote waitlist (#18) will need a more sophisticated disambiguation strategy (e.g., reply with a code like "Y1" or "Y2").

### 9.2 TeeTimeRequest Status Mutation

`TeeTimeRequest` is a domain entity in `Shadowbrook.Domain` with private setters. The `Fulfill()` method must be added to the domain entity. The webhook endpoint needs to load the `TeeTimeRequest` to call this method. Loading path: `WaitlistOffer.TeeTimeRequestId` -> query `TeeTimeRequests` DbSet.

### 9.3 Concurrency on Claim

Two concurrent Y replies for the same offer: the `UNIQUE(WaitlistRequestId)` constraint on `WaitlistRequestAcceptances` prevents double-booking at the DB level. The second `SaveChangesAsync()` will throw `DbUpdateException`. The webhook endpoint should catch this and send the "already claimed" SMS.

### 9.4 Webhook Endpoint Outside Tenant Scope

The webhook endpoint has no tenant context. It operates entirely through the offer record, which contains all denormalized data needed. No query filters are involved since `WaitlistOffers` is queried directly by phone number (a global, non-tenant-scoped query). This is correct - inbound SMS has no concept of tenants.

### 9.5 Response Window Default

The 5-minute response window is hardcoded for this story. Issue #176 (response window configuration) will allow operators to configure this per-course. When that lands, the handler should read from course configuration; until then, use a constant.

### 9.6 Offer vs. Event Handler Timing

The `TeeTimeRequestAddedOfferHandler` runs synchronously within the `SaveChangesAsync()` call that creates the `TeeTimeRequest`. The offer is created and SMS sent before the HTTP response returns to the operator. This is by design for v1 (in-process events). If the SMS send fails, the error is caught and logged by `InProcessDomainEventPublisher` - the tee time request is still created successfully.

---

## 10. Open Questions for Owner

1. **Response window duration**: Is 5 minutes the right default? The lightweight review mentioned 15 minutes for remote waitlist, but walk-up golfers are on-site and should respond faster. Recommend 5 minutes for walk-up.

2. **SMS format preferences**: The proposed messages are casual and concise. Should they include the date in the offer SMS, or is "today" sufficient for walk-up (since walk-up waitlists are same-day only)?

3. **Booking player count**: When creating the `Booking` from an accepted offer, should `PlayerCount` come from the `TeeTimeRequest.GolfersNeeded` or from the `GolferWaitlistEntry.GroupSize`? They may differ. Recommend using `GolferWaitlistEntry.GroupSize` since that represents the actual golfer's group.
