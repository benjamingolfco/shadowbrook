---
name: Denormalize tee time data onto WaitlistOffer
description: WaitlistOffer now carries CourseId, Date, TeeTime from TeeTimeOpening at creation time; CreateBookingHandler no longer queries for the opening
type: project
---

`WaitlistOffer` gained three denormalized properties (`CourseId`, `Date`, `TeeTime`) populated via `GolferWaitlistEntry.CreateOffer(opening, ...)` → `WaitlistOffer.Create(...)`. Both `WaitlistOfferAccepted` and `WaitlistOfferCreated` now carry these flat fields.

`CreateBookingHandler` was simplified to use `evt.CourseId`, `evt.Date`, `evt.TeeTime` directly — `ITeeTimeOpeningRepository` dependency removed.

EF migration `AddTeeTimeDataToWaitlistOffer` adds `CourseId`, `Date`, `TeeTime` columns with an index on `CourseId`.

**Why:** Downstream handlers needed course/date/time data but were forced to re-query `TeeTimeOpening` just to get those values, adding unnecessary DB round-trips and coupling.

**How to apply:** When a new handler reacts to `WaitlistOfferAccepted` and needs course/date/time data, use `evt.CourseId`, `evt.Date`, `evt.TeeTime` directly — no opening lookup needed.
