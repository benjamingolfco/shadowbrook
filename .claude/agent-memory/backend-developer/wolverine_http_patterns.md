---
name: Wolverine HTTP patterns and gotchas
description: Critical patterns learned during Wolverine HTTP migration — body binding, [NotBody], FluentValidation packages
type: project
---

Wolverine HTTP migration revealed several patterns that differ from the documentation's idealized picture:

## POST endpoints without a request body
Wolverine HTTP treats the first "complex type" parameter as the JSON request body. If a POST endpoint has no body (e.g., POST /open), `ApplicationDbContext` will be mistaken for the body, returning 400 "Invalid JSON format". Fix: use `[NotBody]` attribute on the DbContext parameter.

**Why:** Wolverine HTTP's parameter binding convention doesn't check the DI container to disambiguate service types from body types for the first complex parameter.

**How to apply:** Any POST/PUT endpoint that takes `ApplicationDbContext` but has no request body needs `[NotBody]` on the db parameter. Not needed on GET endpoints or when another type already serves as the body.

## FluentValidation requires separate packages for HTTP vs message handlers
`opts.UseFluentValidation()` (on `WolverineOptions`) only applies to message handlers. HTTP endpoints need the separate `WolverineFx.Http.FluentValidation` package AND `opts.UseFluentValidationProblemDetailMiddleware()` on `MapWolverineEndpoints`.

**Why:** Wolverine HTTP and Wolverine messaging have separate middleware pipelines.

**How to apply:** Always verify both packages are referenced and both middleware calls are present. Missing the HTTP package means validators silently don't run on HTTP endpoints.
