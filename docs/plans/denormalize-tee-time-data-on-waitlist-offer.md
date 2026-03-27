# Implementation Plan: Denormalize Tee Time Data onto WaitlistOffer

## Approach

Add `CourseId`, `Date`, and `TeeTime` as denormalized properties on the `WaitlistOffer` entity, populated at creation time from the `TeeTimeOpening`. This eliminates the need for `CreateBookingHandler` (and future handlers) to look up the opening just to get course/date/time data. The `WaitlistOfferAccepted` event will carry these fields directly from the entity's own properties.

The production call site is `GolferWaitlistEntry.CreateOffer(opening, timeProvider)` which delegates to `WaitlistOffer.Create(...)`. The `TeeTimeOpening` is already in scope there, so extracting `CourseId`, `TeeTime.Date`, and `TeeTime.Time` is straightforward.

### GolferWaitlistEntry and CourseId

`GolferWaitlistEntry` has `CourseWaitlistId` (FK to the waitlist it belongs to) but no direct `CourseId`. Denormalizing `CourseId` onto `GolferWaitlistEntry` is **not needed for this change** — the offer gets `CourseId` from the `TeeTimeOpening` at creation time, which is always available. If a future use case needs `CourseId` on the entry itself, that can be a separate effort.

## Files

### Domain Layer

- **Modify:** `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/WaitlistOffer.cs`
  - Add three properties: `CourseId` (Guid), `Date` (DateOnly), `TeeTime` (TimeOnly)
  - Expand `Create()` signature to accept `courseId`, `date`, `teeTime` parameters
  - Populate the new properties in the factory method
  - Include `CourseId`, `Date`, `TeeTime` in the `WaitlistOfferAccepted` event raised by `Accept()`
  - Include `CourseId`, `Date`, `TeeTime` in the `WaitlistOfferCreated` event raised by `Create()` (consistency — downstream handlers may want this data too)

- **Modify:** `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferAccepted.cs`
  - Add `required Guid CourseId`, `required DateOnly Date`, `required TimeOnly TeeTime`

- **Modify:** `src/backend/Shadowbrook.Domain/WaitlistOfferAggregate/Events/WaitlistOfferCreated.cs`
  - Add `required Guid CourseId`, `required DateOnly Date`, `required TimeOnly TeeTime` (keeps events consistent; the data is available at creation)

- **Modify:** `src/backend/Shadowbrook.Domain/GolferWaitlistEntryAggregate/GolferWaitlistEntry.cs`
  - Update `CreateOffer()` to pass `opening.CourseId`, `opening.TeeTime.Date`, `opening.TeeTime.Time` to `WaitlistOffer.Create()`

### Infrastructure Layer

- **Modify:** `src/backend/Shadowbrook.Api/Infrastructure/EntityTypeConfigurations/WaitlistOfferConfiguration.cs`
  - Map `CourseId` as required, with index
  - Map `Date` column
  - Map `TeeTime` column with `HasColumnType("time")`
  - No FK to Course table needed (this is denormalized data, not a navigation — the source of truth is `TeeTimeOpening`)

- **Create:** EF Core migration via `dotnet ef migrations add AddTeeTimeDataToWaitlistOffer --project src/backend/Shadowbrook.Api`

### Handler Layer

- **Modify:** `src/backend/Shadowbrook.Api/Features/Bookings/Handlers/WaitlistOfferAccepted/CreateBookingHandler.cs`
  - Remove `ITeeTimeOpeningRepository` dependency
  - Use `evt.CourseId`, `evt.Date`, `evt.TeeTime` directly instead of looking up the opening
  - Keep `IGolferRepository` for the golfer name lookup (as directed)

### Tests

- **Modify:** `tests/Shadowbrook.Domain.Tests/WaitlistOfferAggregate/WaitlistOfferTests.cs`
  - Update `CreateOffer()` helper to pass `courseId`, `date`, `teeTime`
  - Update `Create_SetsPropertiesAndGeneratesIds` to assert the three new properties
  - Update `Create_RaisesWaitlistOfferCreatedEvent` to assert `CourseId`, `Date`, `TeeTime` on the event
  - Update `Accept_PendingOffer_SetsAcceptedAndRaisesEvent` to assert `CourseId`, `Date`, `TeeTime` on the accepted event

- **Modify:** `tests/Shadowbrook.Domain.Tests/GolferWaitlistEntryAggregate/GolferWaitlistEntryTests.cs`
  - Update `CreateOffer_ReturnsOfferWithCorrectProperties` to assert `offer.CourseId`, `offer.Date`, `offer.TeeTime` match the opening's values

- **Modify:** `tests/Shadowbrook.Api.Tests/Handlers/WaitlistOfferAcceptedCreateBookingHandlerTests.cs`
  - Remove `ITeeTimeOpeningRepository` setup
  - Construct `WaitlistOfferAccepted` events with `CourseId`, `Date`, `TeeTime` fields
  - Remove `Handle_OpeningNotFound_Throws` test (no longer applicable)
  - Update `Handle_CreatesBookingWithCorrectProperties` to verify booking uses event data directly
  - Update handler call to match new signature (no opening repo parameter)

- **Modify:** `tests/Shadowbrook.Api.Tests/Handlers/TeeTimeOpeningFilledRejectOffersHandlerTests.cs`
  - Update `WaitlistOffer.Create()` calls to include the three new parameters

- **Modify:** `tests/Shadowbrook.Api.Tests/Handlers/RejectStaleOfferHandlerTests.cs`
  - Update `CreatePendingOffer()` helper to pass the three new parameters

- **Modify:** `tests/Shadowbrook.Api.Tests/Policies/TeeTimeOpeningOfferPolicyTests.cs`
  - Update any `WaitlistOfferAccepted` event construction to include new required fields

- **Modify:** `tests/Shadowbrook.Api.Tests/Policies/WaitlistOfferResponsePolicyTests.cs`
  - Update any `WaitlistOfferAccepted` event construction to include new required fields

## Patterns

This follows the existing denormalization pattern already used in the codebase. `TeeTimeOpeningClaimed` already carries `CourseId`, `Date`, `TeeTime` (see `TeeTimeOpening.Claim()` lines 83-91). We are applying the same pattern to `WaitlistOfferAccepted`.

Events carry flat fields (not value objects) per the conventions in `backend-conventions.md`: "Events carry flat fields, not value objects."

## Data Model

Pseudocode for WaitlistOffer new properties:

```
CourseId: Guid (required, indexed)
Date: DateOnly (required)
TeeTime: TimeOnly (required, SQL type "time")
```

These are stored as scalar columns on the `WaitlistOffers` table, not as a `ComplexProperty`. They are denormalized copies of data from `TeeTimeOpening` — no FK relationship.

## API Design

No API changes. The `AcceptOffer` endpoint loads the `WaitlistOffer` and calls `offer.Accept()`. The event now carries richer data, but the endpoint contract is unchanged.

## Risks

1. **Migration on existing data**: If the `WaitlistOffers` table has existing rows, the new non-nullable columns need default values in the migration. Since this is pre-production, the simplest approach is to allow the migration to run normally (empty table) or provide sensible defaults. If there are test rows, the migration may need `defaultValue` clauses temporarily.

2. **Event schema change**: `WaitlistOfferAccepted` and `WaitlistOfferCreated` gain required fields. Any in-flight messages in the Wolverine outbox using the old schema would fail to deserialize. This is safe pre-production but would require a versioning strategy in production.

3. **No data consistency enforcement**: The denormalized fields are copies — if a `TeeTimeOpening` were hypothetically updated after offer creation, the offer would retain the original values. This is the desired behavior (the offer reflects what was offered at the time).

## Testing Strategy

- **Domain unit tests**: Verify `WaitlistOffer.Create()` stores `CourseId`, `Date`, `TeeTime` correctly. Verify `Accept()` includes them in the raised event. Verify `GolferWaitlistEntry.CreateOffer()` passes the opening's data through.
- **Handler unit tests**: Verify `CreateBookingHandler` creates a booking using event data (no opening repo). Verify the golfer lookup still works. Remove the "opening not found" test since the opening repo dependency is gone.
- **All existing tests**: Must still pass after updating `WaitlistOffer.Create()` call sites with the new parameters.

## Dev Tasks

### Backend Developer

- [ ] Add `CourseId`, `Date`, `TeeTime` properties to `WaitlistOffer` entity
- [ ] Expand `WaitlistOffer.Create()` signature and populate new properties
- [ ] Add `CourseId`, `Date`, `TeeTime` to `WaitlistOfferAccepted` event record
- [ ] Add `CourseId`, `Date`, `TeeTime` to `WaitlistOfferCreated` event record
- [ ] Update `WaitlistOffer.Accept()` to include new fields in the raised event
- [ ] Update `WaitlistOffer.Create()` to include new fields in the raised `WaitlistOfferCreated` event
- [ ] Update `GolferWaitlistEntry.CreateOffer()` to pass opening data to `WaitlistOffer.Create()`
- [ ] Update `WaitlistOfferConfiguration` EF mapping with new columns and index on `CourseId`
- [ ] Generate EF Core migration
- [ ] Simplify `CreateBookingHandler` to use event data, remove `ITeeTimeOpeningRepository` dependency
- [ ] Update all domain tests for new `Create()` signature and event assertions
- [ ] Update all handler tests for new event shape and handler signature
- [ ] Run `dotnet build shadowbrook.slnx` and `dotnet format shadowbrook.slnx`
- [ ] Run full test suite
