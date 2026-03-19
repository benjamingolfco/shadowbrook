---
name: Wolverine HTTP patterns and gotchas
description: Critical patterns learned during Wolverine HTTP migration — body binding, SaveChangesAsync, domain events, FluentValidation
type: project
---

Wolverine HTTP migration revealed several patterns that differ from the documentation's idealized picture:

## POST endpoints without a request body
Wolverine HTTP treats the first "complex type" parameter as the JSON request body. If a POST endpoint has no body (e.g., POST /open), `ApplicationDbContext` will be mistaken for the body, returning 400 "Invalid JSON format". Fix: use `[NotBody]` attribute on the DbContext parameter.

**Why:** Wolverine HTTP's parameter binding convention doesn't check the DI container to disambiguate service types from body types for the first complex parameter.

**How to apply:** Any POST/PUT endpoint that takes `ApplicationDbContext` but has no request body needs `[NotBody]` on the db parameter.

## SaveChangesAsync must be called explicitly
Wolverine's `UseEntityFrameworkCoreTransactions()` auto-save does NOT work with SQLite in-memory (test environment). The EF Core outbox requires SQL Server tables. As a result:
- HTTP endpoints must call `await db.SaveChangesAsync()` explicitly
- Event handlers must call `await db.SaveChangesAsync()` explicitly
- `ApplicationDbContext` has a `SaveChangesAsync` override that collects `DomainEvents` from change-tracked entities *after* `base.SaveChangesAsync()`, clears them, then publishes each via `IMessageBus.PublishAsync`. This is the mechanism that dispatches domain events in both test and production.
- `opts.PublishDomainEventsFromEntityFrameworkCore<Entity, IDomainEvent>(e => e.DomainEvents)` is also configured in `Program.cs` — it's harmless alongside the manual override because the override clears events before Wolverine's interceptor can scrape them.

**Why:** SQLite in-memory doesn't have Wolverine outbox tables, so the transactional middleware's auto-save is inert in tests. The manual `SaveChangesAsync` override is the load-bearing path for domain event publishing in all environments.

**How to apply:** Always call `SaveChangesAsync` explicitly after mutations. Never remove `IMessageBus` from `ApplicationDbContext` or strip the `SaveChangesAsync` override — the event chain breaks silently if you do.

## FluentValidation is NOT automatic for HTTP endpoints
`opts.UseFluentValidation()` only applies to Wolverine message handlers, NOT HTTP endpoints. Wolverine HTTP has no built-in FluentValidation integration. The old `ValidationFilter` endpoint filter also doesn't work because Wolverine-generated endpoint handlers bypass the ASP.NET endpoint filter pipeline.

**Why:** Wolverine HTTP generates its own handler code that doesn't participate in ASP.NET's `IEndpointFilter` pipeline.

**How to apply:** Inject `IValidator<T>` into endpoint methods and call `validator.ValidateAsync()` explicitly. Return `Results.BadRequest(new { error = ... })` on validation failure.
