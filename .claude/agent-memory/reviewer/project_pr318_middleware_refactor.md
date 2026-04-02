---
name: PR 318 — Remove AppUserEnrichmentMiddleware
description: Outcome and patterns from reviewing the auth middleware-to-claims-transformation refactor on PR 318
type: project
---

Reviewed auth middleware refactor (PR 318, branch `issue/240-authentication-authorization`). Implementation replaced `AppUserEnrichmentMiddleware` with `IClaimsTransformation` + `IAuthorizationHandler` + `IAuthorizationMiddlewareResultHandler`. Implementation was plan-compliant and correct.

**Blocker found:** Missing PR comment justifying removed test assertions (`RecordLogin_UpdatesLastLoginAt` test and `LastLoginAt` assertions) — required by CLAUDE.md test integrity rules. The Architect's plan even pre-wrote the required comment text. Developer did not post it.

**Why:** CLAUDE.md mandates a justification comment on the PR whenever existing assertions or tests are removed, even when the removal is clearly intentional per acceptance criteria.

**How to apply:** On future reviews, always scan for removed/modified assertions in the diff and verify a justification comment exists on the PR. The absence of the comment is a blocker regardless of how obvious the intent is.

Two suggestions (not blockers):
1. `AuthenticatedUser_UnlinkedAppUser_MatchesByEmail_DispatchesSetupCommand` test doesn't assert `app_user_id` claim is present after setup — gap in coverage.
2. `IAuthorizationMiddlewareResultHandler` registered via lambda (`sp => new AppUserAuthorizationResultHandler(new AuthorizationMiddlewareResultHandler())`) instead of typed singleton — functional but bypasses DI for the inner default handler.
