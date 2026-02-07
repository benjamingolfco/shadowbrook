---
name: code-review
description: Review checklists for Shadowbrook PRs. Covers correctness, test coverage, .NET patterns, TypeScript patterns, security (OWASP), performance, and adherence to technical plans.
user-invocable: false
---

# Code Review Checklists

Review criteria for the Shadowbrook tee time booking platform. The Code Reviewer agent loads this skill for its evaluation framework. Criteria are ordered by severity — check correctness first, performance last.

## Correctness & Logic

- Does the code do what the issue and acceptance criteria require?
- Are there logic errors, off-by-one mistakes, or unhandled edge cases?
- Are null/undefined cases handled properly?
- Do error paths return appropriate HTTP status codes and messages?

## Test Coverage

- Are critical paths tested? Check that acceptance criteria from the user story have corresponding tests.
- Are edge cases covered (empty inputs, boundary values, not-found scenarios)?
- Do tests actually assert meaningful behavior, not just "it doesn't throw"?
- Are tests using the project's existing patterns (xUnit, TestWebApplicationFactory, SQLite in-memory)?

## Naming Conventions

- Are names clear, descriptive, and consistent with the rest of the codebase?
- Do endpoint names follow RESTful conventions?
- Do file names match the types they contain?

## Security (OWASP Top 10)

- **Injection:** are user inputs parameterized in queries? No raw SQL concatenation.
- **XSS:** is user-supplied content escaped before rendering?
- **Broken access control:** are endpoints properly authorized?
- **Mass assignment:** are DTOs/request models scoped to only the fields that should be writable?

## Performance

- **N+1 queries:** are related entities loaded efficiently (Include/ThenInclude or projection)?
- **Unnecessary allocations:** large objects in hot paths, string concatenation in loops?
- **Async all the way:** no `.Result` or `.Wait()` on async calls?

## Adherence to Technical Plan

- If the Architect posted a technical plan comment on the issue, verify the implementation follows it.
- Check that the file structure, data model, API design, and testing strategy match the plan.
- If the implementation diverges from the plan, flag it — the deviation may be intentional but should be justified.

## Review Style

- **Be specific.** Reference exact file paths and line numbers. Don't say "the endpoint has an issue" — say "`src/api/Endpoints/BookingEndpoints.cs:42` — the null check on `courseId` is missing."
- **Explain WHY.** Don't just say "change this." Explain the risk or benefit.
- **Distinguish severity.** Clearly mark items as:
  - **Blocker** — must fix before merge (correctness bugs, security issues, missing tests for critical paths)
  - **Suggestion** — nice to have but not blocking (naming improvements, minor refactors, optional optimizations)
- **Respect existing conventions.** If the code follows a pattern already established in the codebase, don't nitpick it even if you'd prefer a different style. Consistency trumps preference.
- **Ask rather than assume.** If something looks wrong but you're not sure about the intent, post a directed question rather than flagging it as an error.
