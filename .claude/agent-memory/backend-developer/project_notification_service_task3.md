---
name: NotificationService Task 3
description: Task 3 implementation of NotificationService with SMS/email routing and unit tests
type: project
---

NotificationService implemented in `Infrastructure/Services/NotificationService.cs`. Routes by checking Golfer table for phone (SMS wins), then AppUser table for email fallback, logging a warning if neither exists. NoOpSmsSender added alongside NoOpEmailSender. All three registered in Program.cs as scoped.

**Why:** INotificationService domain interface needed a concrete implementation that uses the DB to resolve contact info and delegates to ISmsSender/IEmailSender.

**How to apply:** NotificationService uses `IgnoreQueryFilters()` on both Golfer and AppUser queries to bypass the organization scoping filter — necessary since contact info lookup is user-identity based, not tenant-scoped.

Test notes:
- Tests use `UseInMemoryDatabase` with a unique DB name per test class instance
- AppUser factory requires `IAppUserEmailUniquenessChecker` — stub it with NSubstitute returning `false`
- For "prefer SMS" test, EF entry `Property("Id").CurrentValue` override lets you force matching IDs on separately-created domain objects
- `LogWarning` verification on NSubstitute `ILogger` mock must call `.Log(LogLevel.Warning, ...)` directly — the `LogWarning` extension method isn't directly interceptable
