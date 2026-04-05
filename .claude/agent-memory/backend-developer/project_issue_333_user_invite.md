---
name: Issue 333 User Invite Flow - Task 1 Complete
description: Domain events and exception for AppUser invite feature
type: project
---

## Issue 333: User Invite Flow

**Task 1: Domain Events and Exception** ✓ Complete

Created three files in `src/backend/Teeforce.Domain/AppUserAggregate/`:

1. **AppUserCreated.cs** (`Events/`)
   - Record type implementing `IDomainEvent`
   - Properties: `AppUserId`, `Email`, `Role` (all required)
   - Auto-generated `EventId` and `OccurredAt`

2. **AppUserSetupCompleted.cs** (`Events/`)
   - Record type implementing `IDomainEvent`
   - Properties: `AppUserId`, `Email` (both required)
   - Auto-generated `EventId` and `OccurredAt`

3. **IdentityAlreadyLinkedException.cs** (`Exceptions/`)
   - Extends `DomainException`
   - Primary constructor, no params
   - Message: "This user is already linked to a different identity."

**Build Status:** Domain project builds successfully. Events and exception ready for Task 2 implementation.

**Next:** Task 2 will add these events to AppUser aggregate methods and repository implementations.
