---
name: AppUser UpdateRole Test
description: Unit test added for AppUser.UpdateRole method to verify role and organization ID updates
type: reference
---

## Completed Work

Added unit test `UpdateRole_UpdatesRoleAndOrganizationId` to verify the `AppUser.UpdateRole` method correctly updates both the user's role and organization ID.

**Test Location:** `/tests/Shadowbrook.Domain.Tests/AppUserAggregate/AppUserTests.cs`

**Test Details:**
- Creates a test user with Operator role
- Calls `UpdateRole(AppUserRole.Admin, newOrgId)`
- Asserts both `user.Role` and `user.OrganizationId` are updated

**Migration Created:**
- `20260401045209_UpdateAppUserRole.cs` — drops CourseAssignments table from earlier refactoring
- Uses file-scoped namespace per project conventions
- No pending model changes remain

**Test Results:**
- All 7 AppUser domain tests pass
- All 110 domain tests pass
- All 161 API unit tests pass
- Integration test failures (2) are pre-existing and unrelated to this work

**Commit:** `a797c44` — "refactor: simplify role model — rename Owner to Operator, remove Staff and CourseAssignment"
