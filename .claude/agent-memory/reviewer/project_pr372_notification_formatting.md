---
name: PR 372 notification formatting review
description: Blockers found in notification formatting + Wolverine dispatch implementation — dead code types, inline exceptions, GolferId/appUserId mismatch
type: project
---

PR 372 (`issue/notification-service`) — spec compliance review of notification formatting + Wolverine command dispatch.

**Why:** Reviewed against `docs/superpowers/plans/2026-04-06-notification-formatting.md`.

**Blockers found:**

1. **Duplicate notification types — Task 4 files are dead code.** The plan had Task 4 create 6 dedicated notification type files (e.g., `BookingConfirmation.cs`) and Task 7 have the handlers *use* those types. The implementation instead created Task 4 files as specified *and* a second parallel set of `*Notification`-suffixed types colocated in the handler files. Program.cs registers the `*Notification` types; `SmsFormatterTests` tests the Task 4 types. Both sets exist; only one is wired up.

2. **`InvalidOperationException` thrown inline in two handlers** (`ConfirmationSmsHandler.cs:32`, `GolferJoinedWaitlist/SmsHandler.cs:30`) — violates the "always use `GetRequiredByIdAsync`" convention.

3. **Handlers pass `GolferId` as `appUserId` parameter** — 4 of 6 handlers pass `GolferId` to `INotificationService.Send(appUserId, ...)`. Works mechanically via fallback, but violates the interface contract and could silently skip AppUser phone lookup for dual-identity users.

**Suggestion:** `AppUser.Phone` has no domain mutation method — tests use reflection to set it, flagging a missing feature for real callers.

**How to apply:** When reviewing future notification or domain service work, watch for: duplicate type families at different layers, inline `?? throw` in handlers, and ID type mismatches at service boundaries.
