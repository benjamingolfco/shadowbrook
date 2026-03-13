---
name: Waitlist offer claim pattern
description: Architecture decisions for walk-up waitlist offer/claim flow (issue #37) -- token-based unauthenticated access, broadcast vs cascade, lazy expiration
type: project
---

For issue #37 (walk-up golfer claims slot), the following architectural decisions were made:

- **WaitlistOffer entity** is an infrastructure model in `Models/` (not a domain aggregate) for SMS token tracking. It denormalizes course name, date, tee time, golfer info to avoid joins on the unauthenticated endpoint.
- **Token != Id**: `WaitlistOffer.Token` is a separate GUID from `Id`. Token is the credential exposed in SMS URLs; Id is for internal references.
- **Broadcast model for walk-up**: ALL eligible golfers get SMS simultaneously when an operator creates a tee time request. First to tap wins. This differs from the future remote waitlist (#3) which will use FIFO cascade.
- **Lazy expiration**: No background job. Offers are expired on read (GET) and accept (POST) by checking `ExpiresAt` against server time.
- **Unauthenticated endpoints**: `GET/POST /waitlist/offers/{token}` are outside the tenant-scoped `api` group. No auth middleware. Token IS the credential.
- **WaitlistRequestAcceptance** is the junction between TeeTimeRequest and GolferWaitlistEntry, per the waitlist architecture doc.
- **Concurrency**: Unique constraint on `(WaitlistRequestId, GolferWaitlistEntryId)` + optimistic count check against `GolfersNeeded`.
- **In-app SMS for v1**: Uses `InMemoryTextMessageService`. Link in SMS body uses `App:BaseUrl` from config.
- **Event chain**: `TeeTimeRequestAdded` -> `TeeTimeRequestAddedNotifyHandler` (creates offers, sends SMS) -> golfer accepts -> endpoint publishes `WaitlistOfferAccepted` -> `WaitlistOfferAcceptedHandler` (creates booking, removes from waitlist, sends confirmation).
