# Memory Index

- [project_denormalize_waitlist_offer.md](project_denormalize_waitlist_offer.md) — Added CourseId, Date, TeeTime to WaitlistOffer and both events; simplified CreateBookingHandler to use event data directly
- [project_organization_entity_and_rename.md](project_organization_entity_and_rename.md) — Organization aggregate added; Course.TenantId renamed to OrganizationId; ICurrentUser.TenantId unchanged pending later task
- [project_auth_endpoint_authorization.md](project_auth_endpoint_authorization.md) — Authorization attributes on all endpoints; test helper patterns (CreateAuthenticatedClient, SeedTestAdminAsync); body TenantId vs X-Tenant-Id header behavior
- [appuser_updaterole_test.md](appuser_updaterole_test.md) — Unit test for AppUser.UpdateRole method; migration drops CourseAssignments table; all unit tests pass
- [project_issue_333_user_invite.md](project_issue_333_user_invite.md) — Task 1: AppUserCreated, AppUserSetupCompleted events and IdentityAlreadyLinkedException created
- [feedback_integration_tests_sandbox.md](feedback_integration_tests_sandbox.md) — Integration tests always fail in sandbox (Docker blocked); unit tests passing is sufficient to confirm correctness
