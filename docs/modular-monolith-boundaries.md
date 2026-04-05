# Modular Monolith — Domain Boundaries

## Motivation

The primary driver is **agent safety**: when AI agents work on features, project-level boundaries prevent them from accidentally reaching across modules — querying data they shouldn't, placing handlers in the wrong feature, or creating tight coupling that shouldn't exist. Compile-time enforcement is stronger than convention.

Secondary benefits:
- Code review becomes simpler ("this PR touches two modules" is an immediate flag)
- Forces explicit contracts between modules via events/commands
- Aligns with the event-driven architecture principle already in place
- Makes ownership and responsibility clearer as the codebase grows

## Proposed Modules

### 1. Administration (Tenants + Courses)

**Aggregates**: Tenant, Course

Setup and configuration context. No domain events, no runtime behavior. Both are pure CRUD — courses belong to tenants, and neither participates in event choreography.

### 2. Golfers

**Aggregates**: Golfer

Identity context. Golfer profiles, phone numbers, preferences. Golfers are global (not tenant-scoped), referenced by both Bookings and Waitlist but owned by neither. Currently lives awkwardly inside Waitlist feature code.

### 3. Waitlist

**Aggregates**: CourseWaitlist (WalkUp/Online), GolferWaitlistEntry, TeeTimeOpening, WaitlistOffer
**Policies**: TeeTimeOpeningOfferPolicy, WaitlistOfferResponsePolicy, TeeTimeOpeningExpirationPolicy
**Domain Services**: WaitlistMatchingService

The demand pool — queuing, matching, offering. This is the complex core of the system. These aggregates are tightly coupled through the offer lifecycle; splitting TeeTimeOpening or WaitlistOffer out would just create unnecessary chattiness between modules.

### 4. Bookings

**Aggregates**: Booking
**Policies**: BookingConfirmationPolicy

Reservation context — creating, confirming, rejecting, cancelling bookings.

Cross-module events consumed:
- `WaitlistOfferAccepted` → creates a booking
- `TeeTimeOpeningClaimed` / `TeeTimeOpeningClaimRejected` → confirms or rejects

Cross-module events published:
- `BookingCreated` → triggers slot claim in Waitlist
- `BookingCancelled` → triggers offer rejection in Waitlist

### 5. Notifications

**Services**: ITextMessageService implementations
**Handlers**: All SMS handlers currently scattered across Bookings and Waitlist

Delivery channel context. Notification handlers don't own any aggregates — they react to events and send messages. Currently SMS logic lives in both Bookings and Waitlist features. Extracting it means notification concerns don't couple to business rules, and adding email/push later has a natural home.

### 6. TeeSheet (Read Model)

Query/projection context for the operator dashboard. No writes, just cross-cutting reads. Already isolated.

## The Critical Boundary: Waitlist <-> Bookings

This is the hardest boundary because of the bidirectional event cycle:

```
Waitlist: OfferAccepted →
  Bookings: CreateBooking → BookingCreated →
    Waitlist: ClaimSlot → TeeTimeOpeningClaimed →
      Bookings: ConfirmBooking
```

This actually works well as separate modules because every step is already a separate event/handler. The coupling is through messages, not shared state. With separate projects:

- Bookings can't accidentally query WaitlistOffers or TeeTimeOpenings directly
- Waitlist can't accidentally modify Booking status directly
- The shared contract is just the event/command types

## Project Structure

```
src/backend/
  Teeforce.Domain/                    # Aggregates, events, repository interfaces
  Teeforce.Contracts/                 # Shared event/command types for cross-module messaging
  Teeforce.Administration/            # Tenant + Course endpoints, repos
  Teeforce.Golfers/                   # Golfer endpoints, repos
  Teeforce.Waitlist/                  # Endpoints, handlers, policies, repos
  Teeforce.Bookings/                  # Endpoints, handlers, policies, repos
  Teeforce.Notifications/            # All SMS/notification handlers
  Teeforce.TeeSheet/                 # Read model queries
  Teeforce.Api/                       # Host — wires modules, shared infra, DbContext, middleware
```

Each module project references `Domain` and `Contracts` but **not each other**. The `Api` host project references all modules and registers their services.

## Open Questions

### Single vs. split Domain project

**Option A — Single Domain project (pragmatic start):**
Keep `Teeforce.Domain` as one project. Module projects reference it but can only use the aggregates/interfaces relevant to them by convention. The compiler won't stop a Waitlist handler from importing `Booking`, but the project boundary at the API layer still prevents most accidental coupling.

**Option B — Split Domain per module (strongest isolation):**
Each module gets its own domain project (e.g., `Teeforce.Waitlist.Domain`). Shared types like the `Entity` base class, `TeeTime` value object, and common interfaces move to `Teeforce.Domain.Common`. This gives compile-time guarantees at the domain level too, at the cost of more project plumbing.

Recommendation: Start with Option A. If agents repeatedly reach across domain boundaries despite the API-layer split, upgrade to Option B.

### Single vs. split DbContext

**Option A — Single shared DbContext:**
One `ApplicationDbContext` in the host project, used by all modules. Module boundaries are enforced by project references (each module only has access to its own repository implementations). Wolverine's transactional middleware works normally with one DbContext per handler.

**Option B — DbContext per module:**
Each module owns its own DbContext with only its tables. Gives hard data isolation but hits Wolverine's limitation: you can't use more than one DbContext in the same handler with transactional middleware. Cross-module handlers would need manual transaction management.

Recommendation: Option A. The transactional middleware limitation makes split DbContexts painful, and the agent guardrail goal is better served by project-level separation than DbContext-level separation.

### Shared Contracts project

Domain events that cross module boundaries need a shared home. Options:
- **`Teeforce.Contracts/`**: A thin project with just event/command record types. Both publisher and consumer reference it.
- **Events stay in `Teeforce.Domain/`**: Simpler if keeping a single domain project — events are already there.

If we go with a single Domain project (Option A above), a separate Contracts project isn't strictly needed. If we split the domain per module, Contracts becomes necessary.

## Wolverine Considerations

- Each command dispatched via `IMessageBus.InvokeAsync()` gets its own handler pipeline, including its own transactional middleware. Two commands to two modules = two independent transactions, not one atomic unit.
- For cases requiring atomicity across modules, options are: keep it in one handler (defeats the split for that case), use a Wolverine saga/policy to coordinate with compensation, or accept eventual consistency.
- The existing saga/policy pattern already handles the Waitlist-Bookings coordination this way — the architecture is already designed for eventual consistency across these boundaries.
