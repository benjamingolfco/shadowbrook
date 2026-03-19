---
name: wolverine_integration_review
description: Outcome and issues found when reviewing the Wolverine integration PR on branch issue/37-claim-tee-time-slot
type: project
---

Reviewed Wolverine integration (branch: issue/37-claim-tee-time-slot) against spec at `docs/superpowers/specs/2026-03-17-wolverine-integration-design.md`. Three blockers found.

**Why:** This was a 1:1 behavioral replacement of `InProcessDomainEventPublisher` with WolverineFx for handler discovery, retry policies, and a path to Azure Service Bus.

**How to apply:** When reviewing future Wolverine-related PRs, check these three areas first as they were missed:

1. **TestWebApplicationFactory not updated** — `DisableAllExternalWolverineTransports()` and `RunWolverineInSoloMode()` were not added. Integration tests will attempt SQL Server transport against SQLite. The `WolverineFx` package was added to the test project but the factory itself was skipped.

2. **Old infrastructure files not deleted** — `IDomainEventPublisher.cs`, `IDomainEventHandler.cs`, `InProcessDomainEventPublisher.cs` in `Infrastructure/Events/` were left on disk despite no remaining references. Spec called for deletion.

3. **Wrong SQL Server API** — Spec required `UseSqlServerPersistenceAndTransport(connStr, "wolverine").AutoProvision()`. Implementation used `PersistMessagesWithSqlServer(connStr, "wolverine").EnableMessageTransport()`. Missing `.AutoProvision()` means Wolverine schema tables are not auto-created on fresh deployments.

What passed: all 11 handler migrations (interface drop, method rename, CancellationToken fix), `ApplicationDbContext` IMessageBus replacement, `WaitlistOfferAcceptedFillHandler` manual retry removal, `MultipleHandlerBehavior.Separated`, concurrency retry policy shape.
