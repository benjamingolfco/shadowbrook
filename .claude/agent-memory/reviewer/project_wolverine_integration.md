---
name: wolverine_integration_review
description: Historical review findings from initial Wolverine integration — resolved. Kept for pattern awareness only.
type: project
---

Reviewed initial Wolverine integration (branch: issue/37-claim-tee-time-slot, 2026-03-17). Three blockers were found and subsequently resolved during the HTTP migration phase.

**Resolved issues (for historical awareness):**

1. **TestWebApplicationFactory** — Was not updated for Wolverine. Now uses Testcontainers SQL Server with full Wolverine pipeline support.

2. **Old infrastructure files** — `IDomainEventPublisher.cs`, `IDomainEventHandler.cs`, `InProcessDomainEventPublisher.cs` were left on disk. Now deleted.

3. **SQL Server API** — Was using wrong persistence API and missing `.AutoProvision()`. Now uses `UseSqlServerPersistenceAndTransport(connStr, "wolverine").AutoProvision()`.

**How to apply:** When reviewing Wolverine-related PRs, verify: (1) TestWebApplicationFactory uses SQL Server, (2) no manual `SaveChangesAsync()` calls in endpoints/handlers, (3) FluentValidation uses correct package for HTTP vs messaging.
