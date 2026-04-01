---
name: WaitlistOffer.Create made internal
description: WaitlistOffer.Create changed from public to internal; tests migrated to use GolferWaitlistEntry.CreateOffer(opening, timeProvider)
type: project
---

`WaitlistOffer.Create` was made `internal static` — external callers must go through `GolferWaitlistEntry.CreateOffer(opening, timeProvider)`.

**Why:** Encapsulate construction within the domain — `Create` is only meaningful when called from `GolferWaitlistEntry` which provides the correct opening/golfer context. Making it internal prevents test code (and any other callers) from bypassing the factory.

**How to apply:** Any test that needs a `WaitlistOffer` must first create a `GolferWaitlistEntry` via the public domain API (`WalkUpWaitlist.OpenAsync` + `waitlist.Join(...)`), then call `entry.CreateOffer(opening, timeProvider)`. A shared helper `WaitlistTestHelpers` was added in `tests/Shadowbrook.Api.Tests/Features/Waitlist/Handlers/` for the three handler tests. Domain.Tests classes set up this infrastructure inline in their own `CreateEntryAsync` / `CreateOfferAsync` helpers.

The `WalkUpWaitlist.Join` helper requires these stubs: `IShortCodeGenerator` (returns any string), `ICourseWaitlistRepository` (returns null for existing waitlist), `IGolferWaitlistEntryRepository` (returns null for active entry), `ITimeProvider` with `GetCurrentTimeByTimeZone` and `GetCurrentDateByTimeZone` returning the opening's time/date.
