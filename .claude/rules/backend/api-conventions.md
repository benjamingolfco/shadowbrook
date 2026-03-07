---
paths:
  - "src/backend/Shadowbrook.Api/**/*.cs"
---

# Backend API Conventions

## Code Style

`.editorconfig` at repo root defines all C# style rules (suggestion severity). Key conventions:

- Prefer `var` over explicit types
- Always use braces on control flow statements
- Private fields use `camelCase` (no underscore prefix), qualified with `this.`
- Prefer target-typed `new()` and primary constructors for DI classes
- File-scoped namespaces, nullable reference types, implicit usings
- `is null` / `is not null` for null checks (not `== null`)
- Always explicit accessibility modifiers
- String interpolation over concatenation

## API Patterns

- Minimal API endpoints in `src/backend/Shadowbrook.Api/Endpoints/`, extension method pattern (`MapXxxEndpoints`)
- Inline DTOs as records within endpoint files
- `Results.*` return pattern (`Results.Ok()`, `Results.BadRequest()`, `Results.NotFound()`)
- Multi-tenant scoping via `ICurrentUser.TenantId` and EF query filters
