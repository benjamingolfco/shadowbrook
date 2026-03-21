---
name: Test improvements review (2026-03-20)
description: Review of chore/wolverine-http-migration branch test improvements — Respawn/Testcontainers migration, validator unit tests, handler unit tests, skipped WaitlistOfferEndpointsTests
type: project
---

Branch `chore/wolverine-http-migration` introduced a major test infrastructure upgrade:

- Replaced SQLite in-memory DB with SQL Server Testcontainers + Respawn for per-test reset
- Added `[Collection("Integration")]` + `IAsyncLifetime.InitializeAsync => ResetDatabaseAsync()` pattern across all integration test classes
- Added 8 validator unit test files and 6 handler unit test files covering the saga chain
- Skipped 13 `WaitlistOfferEndpointsTests` that depend on Wolverine processing `TeeTimeRequestAdded` asynchronously
- Removed 17+ integration tests that were validating FluentValidation rules now covered by unit tests

**Issues found:**
- `Handle_OfferNotFound_DoesNothing` and `Handle_EntryNotFound_DoesNothing` in `BookingCreatedRemoveFromWaitlistHandlerTests`, plus `Handle_NoPendingOffers_DoesNothing` in `TeeTimeRequestFulfilledHandlerTests`, and `Handle_OfferNotFound_DoesNothing` in `TeeTimeSlotFillFailedHandlerTests` are "no-throw" tests with no assertions — they should add `DidNotReceive()` assertions to be meaningful.
- `IntegrationTestAttribute.cs` (new file) is unused — no test class or method applies `[IntegrationTest]`. Appears to be a leftover artifact.
- Respawn lazy-init in `ResetDatabaseAsync` is fine but the first call creates the Respawner against the DB before any migrations have run. Because `CreateHost` (which calls `db.Database.Migrate()`) runs before tests, this is actually safe — but it's a subtle ordering dependency worth noting.
- 13 skipped tests are well-justified: the skip reason accurately describes the root cause (async Wolverine handler timing), the tests themselves are well-written and preserved for future re-enablement.
- All removed integration tests were genuine duplicates — validator behavior is now covered by unit tests; DB-specific behavior (duplicate name conflict, cross-tenant isolation) remains as integration tests.

**Why:** Migrated from SQLite to SQL Server Testcontainers to match production DB (previous SQLite mismatch caused coverage gaps). Pyramid shift intentional.
**How to apply:** The `DidNotReceive()` pattern for "DoesNothing" early-exit tests is now established convention in this codebase.
