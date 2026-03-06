# Implementation Plan: Issue #31 -- Golfer Joins Walk-Up Waitlist

**Issue:** #31 (As a golfer, I can join a walk-up waitlist at the course)
**Parent:** #29 (walk-up waitlist family)
**Depends on:** #180 (operator walk-up waitlist -- merged)
**Date:** 2026-03-06
**Story Points:** 8

---

## 1. Overview

This story delivers the golfer-facing flow for joining a walk-up waitlist. A golfer navigates to `/join` on their mobile browser, enters the 4-digit short code posted at the course, provides their name and phone number, and joins the general walk-up queue. An SMS confirmation is sent with their queue position.

Key architectural decisions from the owner:
- **Introduce the `Golfer` entity in this story** so `GolferId` is required (non-nullable) on `GolferWaitlistEntry` from day one
- Golfer is a minimal phone-keyed identity stub (no passwords, no account)
- Lookup-or-create pattern: normalize phone to E.164, query by phone, create if not found
- This is the **first unauthenticated API surface** -- public endpoints that bypass tenant middleware

---

## 2. Data Model

### 2.1 New Entity: Golfer

Minimal identity stub keyed by phone number. No authentication, no passwords.

```
Golfer
  Id            Guid, PK
  Phone         string, required, max 20, unique index (E.164 format: "+15551234567")
  FirstName     string, required, max 100
  LastName      string, required, max 100
  CreatedAt     DateTimeOffset
  UpdatedAt     DateTimeOffset
```

**File:** `src/api/Models/Golfer.cs`

### 2.2 New Entity: GolferWaitlistEntry

A golfer's presence on a course's daily waitlist. `GolferId` is required (non-nullable) from day one per the owner's decision.

```
GolferWaitlistEntry
  Id                  Guid, PK
  CourseWaitlistId    Guid, FK -> CourseWaitlist, required
  GolferId            Guid, FK -> Golfer, required (non-nullable from day one)
  GolferName          string, required, max 200 (denormalized: "FirstName LastName")
  GolferPhone         string, required, max 20 (denormalized: E.164)
  IsWalkUp            bool, required (default: true for this story)
  IsReady             bool, required (default: true)
  JoinedAt            DateTimeOffset, required
  RemovedAt           DateTimeOffset, nullable (soft delete)
  CreatedAt           DateTimeOffset
  UpdatedAt           DateTimeOffset

  Index: (CourseWaitlistId, GolferPhone) -- for duplicate prevention
  Index: (CourseWaitlistId, GolferId) -- for lookups by golfer
  Index: (CourseWaitlistId, IsWalkUp, IsReady) -- for matching queries
```

**File:** `src/api/Models/GolferWaitlistEntry.cs`

**Design notes:**
- `GolferName` and `GolferPhone` are denormalized copies that survive golfer record changes and avoid joins for operator display and SMS delivery.
- `RemovedAt` enables soft-delete. Active entries: `RemovedAt IS NULL`.
- `WaitingFrom` and `WaitingUntil` from the architecture doc are **omitted** for this story. Walk-up golfers join the general queue, not a time-specific window. These fields will be added when the remote waitlist story (#3) needs them.
- No unique DB constraint on `(CourseWaitlistId, GolferPhone)` because a golfer could leave and rejoin. Duplicate prevention is enforced at the application level by checking `RemovedAt IS NULL`.

### 2.3 EF Core Configuration

**File:** `src/api/Data/ApplicationDbContext.cs` -- add to `OnModelCreating`:

```
// Golfer configuration
Golfer -> HasIndex(g => g.Phone).IsUnique()
Golfer -> Property(g => g.Phone).HasMaxLength(20)
Golfer -> Property(g => g.FirstName).HasMaxLength(100)
Golfer -> Property(g => g.LastName).HasMaxLength(100)

// GolferWaitlistEntry configuration
GolferWaitlistEntry -> HasOne(e => e.CourseWaitlist)
    .WithMany(cw => cw.GolferWaitlistEntries)
    .HasForeignKey(e => e.CourseWaitlistId)
    .OnDelete(Cascade)

GolferWaitlistEntry -> HasOne(e => e.Golfer)
    .WithMany()
    .HasForeignKey(e => e.GolferId)
    .OnDelete(Cascade)

GolferWaitlistEntry -> HasIndex(e => new { e.CourseWaitlistId, e.GolferPhone })
GolferWaitlistEntry -> HasIndex(e => new { e.CourseWaitlistId, e.GolferId })
GolferWaitlistEntry -> HasIndex(e => new { e.CourseWaitlistId, e.IsWalkUp, e.IsReady })
GolferWaitlistEntry -> Property(e => e.GolferName).HasMaxLength(200)
GolferWaitlistEntry -> Property(e => e.GolferPhone).HasMaxLength(20)
```

Also add `DbSet` properties:
```
public DbSet<Golfer> Golfers => Set<Golfer>();
public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();
```

### 2.4 Navigation Property Additions

**`CourseWaitlist.cs`** -- add navigation collection:
```csharp
public ICollection<GolferWaitlistEntry> GolferWaitlistEntries { get; set; } = new List<GolferWaitlistEntry>();
```

### 2.5 Migration

**Migration name:** `AddGolferAndWaitlistEntries`

Creates:
- `Golfers` table with unique index on `Phone`
- `GolferWaitlistEntries` table with FK to `CourseWaitlists` and `Golfers`, plus three composite indexes

Command: `dotnet ef migrations add AddGolferAndWaitlistEntries --project src/api`

---

## 3. Backend Implementation

### 3.1 Phone Normalization Utility

**Create:** `src/api/Services/PhoneNormalizer.cs`

A static utility class that normalizes US phone numbers to E.164 format.

```
public static class PhoneNormalizer
{
    // Returns normalized E.164 string or null if invalid
    public static string? Normalize(string? input)

    // Strips all non-digit characters
    // If 10 digits, prepend "+1" (US)
    // If 11 digits starting with "1", prepend "+"
    // If starts with "+1" and has 11 digits after stripping non-digits from everything after "+", valid
    // Otherwise return null (invalid)

    public static bool IsValid(string? input) => Normalize(input) is not null;
}
```

**Rules:**
- Strip all non-digit characters except leading `+`
- 10 digits -> assume US, prepend `+1` -> `+1XXXXXXXXXX`
- 11 digits starting with `1` -> prepend `+` -> `+1XXXXXXXXXX`
- Already `+1` followed by 10 digits -> valid
- Everything else -> null (invalid)
- Result is always `+1XXXXXXXXXX` (12 chars) for US numbers

### 3.2 Public Walkup Join Endpoints

**Create:** `src/api/Endpoints/WalkupJoinEndpoints.cs`

This is a **new endpoint file** for public (unauthenticated) golfer-facing endpoints. These endpoints do NOT require tenant context and are mapped separately from tenant-scoped endpoints.

```
public static class WalkupJoinEndpoints
{
    public static void MapWalkupJoinEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/walkup");

        group.MapPost("/verify", VerifyShortCode);
        group.MapPost("/join", JoinWaitlist);
    }
}
```

#### POST /walkup/verify

Validates the 4-digit short code and returns course info.

**Request:** `{ "code": "4827" }`
**Success (200):** `{ "courseWaitlistId": "guid", "courseName": "Pine Valley Golf Club", "shortCode": "4827" }`
**Errors:**
- 400: Missing or malformed code (not 4 digits)
- 404: `{ "error": "Code not found or waitlist is not active." }` -- no active waitlist with this code today
- 429: Rate limited (future, see section 3.6)

**Implementation:**
1. Validate code is exactly 4 digits
2. Query `CourseWaitlists` where `ShortCode == code AND Date == today AND Status == "Open"`, include `Course`
3. Use `.IgnoreQueryFilters()` on the query since there is no tenant context (this is a public endpoint)
4. If not found, return 404
5. Return course name and waitlist ID

#### POST /walkup/join

Joins the golfer to the waitlist.

**Request:**
```json
{
  "courseWaitlistId": "guid",
  "firstName": "John",
  "lastName": "Smith",
  "phone": "555-123-4567"
}
```

**Success (201):**
```json
{
  "entryId": "guid",
  "golferName": "John Smith",
  "position": 3,
  "courseName": "Pine Valley Golf Club"
}
```

**Errors:**
- 400: Validation errors (missing fields, invalid phone)
- 404: Waitlist not found or not open
- 409: `{ "error": "You're already on the waitlist.", "position": 2 }` -- duplicate phone for this waitlist

**Implementation:**
1. Validate all required fields present
2. Normalize phone via `PhoneNormalizer.Normalize()` -- return 400 if invalid
3. Look up `CourseWaitlist` by ID with `.IgnoreQueryFilters()`, include `Course`
4. Verify waitlist exists and `Status == "Open"`
5. Check for existing active entry: `GolferWaitlistEntries.Where(e => e.CourseWaitlistId == id && e.GolferPhone == normalizedPhone && e.RemovedAt == null)`
6. If duplicate found, return 409 with their current position
7. **Golfer lookup-or-create:**
   a. Query `Golfers.FirstOrDefault(g => g.Phone == normalizedPhone)` using `.IgnoreQueryFilters()`
   b. If not found, create new `Golfer { Phone, FirstName, LastName }`
   c. If found, optionally update FirstName/LastName if they differ (the golfer may provide updated info)
   d. Handle unique index race condition: wrap in try/catch for `DbUpdateException` on the phone unique index -- if caught, re-query to get existing golfer
8. Create `GolferWaitlistEntry` with `GolferId = golfer.Id`, denormalized fields, `IsWalkUp = true`, `IsReady = true`
9. `SaveChangesAsync()`
10. Calculate position: count active entries where `JoinedAt <= this entry's JoinedAt` and `RemovedAt IS NULL`
11. Publish `GolferJoinedWaitlist` domain event
12. Return 201 with entry details

### 3.3 Domain Event: GolferJoinedWaitlist

**Create:** `src/api/Events/GolferJoinedWaitlist.cs`

```
public record GolferJoinedWaitlist : IDomainEvent
{
    Guid EventId
    DateTimeOffset OccurredAt
    required Guid GolferWaitlistEntryId
    required Guid CourseWaitlistId
    required Guid GolferId
    required string GolferName
    required string GolferPhone  // E.164
    required string CourseName
    required int Position
}
```

### 3.4 SMS Event Handler: GolferJoinedWaitlistSmsHandler

**Create:** `src/api/Events/GolferJoinedWaitlistSmsHandler.cs`

```
public class GolferJoinedWaitlistSmsHandler : IDomainEventHandler<GolferJoinedWaitlist>
{
    // Inject ITextMessageService

    async Task HandleAsync(GolferJoinedWaitlist event, CancellationToken ct)
    {
        var message = $"You're #{event.Position} on the waitlist at {event.CourseName}. " +
                      "Keep your phone handy - we'll text you when a spot opens up!";
        await textMessageService.SendAsync(event.GolferPhone, message, ct);
    }
}
```

### 3.5 DI Registration

**Modify:** `src/api/Program.cs`

Add after existing DI registrations:
```csharp
builder.Services.AddScoped<IDomainEventHandler<GolferJoinedWaitlist>, GolferJoinedWaitlistSmsHandler>();
```

Add endpoint mapping (BEFORE the tenant middleware or use a separate route group -- see section 3.7):
```csharp
app.MapWalkupJoinEndpoints();
```

### 3.6 Rate Limiting

For the initial implementation, rate limiting is **deferred**. The 4-digit code space is small (10,000 codes) but:
- Codes are only valid for one day
- The code must match an active waitlist (Status == "Open")
- Brute-forcing would require ~5,000 requests on average to find a valid code
- At MVP scale, this is acceptable risk

**Future:** Add `Microsoft.AspNetCore.RateLimiting` with a fixed window limiter on `/walkup/verify` (e.g., 10 requests per minute per IP). This can be a separate follow-up issue.

### 3.7 Tenant Middleware Bypass

The existing `TenantClaimMiddleware` reads `X-Tenant-Id` from headers and adds it as a claim. For public `/walkup/*` endpoints, there is no tenant header -- and that is fine. The middleware already handles the missing header gracefully (it just skips adding the claim). However, EF query filters use `_currentUser.TenantId` which would be null, potentially causing issues.

**Solution:** Use `.IgnoreQueryFilters()` explicitly in the walkup join endpoints when querying `CourseWaitlist` (which joins to `Course` which has a tenant query filter). This is safe because:
- The short code lookup is scoped to today's date and the specific code
- No tenant-scoped data can leak through this path
- The `Golfer` entity has no tenant scope (golfers are global)

The endpoint mapping in `Program.cs` should be placed alongside other endpoint mappings. No middleware changes needed.

### 3.8 Updating GET /walkup-waitlist/today for Operator View

**Modify:** `src/api/Endpoints/WalkUpWaitlistEndpoints.cs`

The existing `GetToday` endpoint returns an empty `Entries` list. Update it to query actual `GolferWaitlistEntry` records:

```
// In GetToday method, after fetching the waitlist:
var entries = waitlist is not null
    ? await db.GolferWaitlistEntries
        .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
        .OrderBy(e => e.JoinedAt)
        .Select(e => new WalkUpWaitlistEntryResponse(e.Id, e.GolferName, e.JoinedAt))
        .ToListAsync()
    : new List<WalkUpWaitlistEntryResponse>();
```

This makes the operator's walk-up waitlist page show real golfer entries.

---

## 4. Frontend Implementation

### 4.1 Feature Folder Structure

```
src/web/src/features/walkup/
  index.tsx              -- Feature entry point (exports WalkupJoinPage)
  pages/
    WalkupJoinPage.tsx   -- Three-phase state machine page
  components/
    CodeEntry.tsx         -- Phase 1: 4-digit code input
    JoinForm.tsx          -- Phase 2: name + phone form
    Confirmation.tsx      -- Phase 3: success confirmation
  hooks/
    useWalkupJoin.ts      -- TanStack Query hooks for verify + join
  __tests__/
    WalkupJoinPage.test.tsx
    CodeEntry.test.tsx
    JoinForm.test.tsx
    Confirmation.test.tsx
```

### 4.2 Router Configuration

**Modify:** `src/web/src/app/router.tsx`

Add a new public route that does NOT use `AuthGuard` or `RoleGuard`:

```tsx
const WalkupFeature = lazy(() => import('@/features/walkup'));

// Add to router array (before or after existing routes):
{
  path: '/join',
  element: (
    <LazyFeature><WalkupFeature /></LazyFeature>
  ),
},
```

### 4.3 Query Keys

**Modify:** `src/web/src/lib/query-keys.ts`

Add:
```typescript
walkupJoin: {
  verify: (code: string) => ['walkup-join', 'verify', code] as const,
},
```

### 4.4 Type Definitions

**Modify:** `src/web/src/types/waitlist.ts`

Add:
```typescript
// Walkup join (public/golfer-facing)
export interface VerifyCodeResponse {
  courseWaitlistId: string;
  courseName: string;
  shortCode: string;
}

export interface JoinWaitlistRequest {
  courseWaitlistId: string;
  firstName: string;
  lastName: string;
  phone: string;
}

export interface JoinWaitlistResponse {
  entryId: string;
  golferName: string;
  position: number;
  courseName: string;
}

export interface DuplicateEntryError {
  error: string;
  position: number;
}
```

### 4.5 Hooks

**Create:** `src/web/src/features/walkup/hooks/useWalkupJoin.ts`

```typescript
import { useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import type { VerifyCodeResponse, JoinWaitlistRequest, JoinWaitlistResponse } from '@/types/waitlist';

export function useVerifyCode() {
  return useMutation({
    mutationFn: (code: string) =>
      api.post<VerifyCodeResponse>('/walkup/verify', { code }),
  });
}

export function useJoinWaitlist() {
  return useMutation({
    mutationFn: (data: JoinWaitlistRequest) =>
      api.post<JoinWaitlistResponse>('/walkup/join', data),
  });
}
```

**Note:** Both are mutations (not queries) because verify is a POST (to support future rate limiting) and join is a POST. Neither needs caching.

### 4.6 Page Component: WalkupJoinPage

**Create:** `src/web/src/features/walkup/pages/WalkupJoinPage.tsx`

Three-phase state machine using `useState`:

```typescript
type Phase = 'code' | 'join' | 'confirmation';

// State:
const [phase, setPhase] = useState<Phase>('code');
const [verifyData, setVerifyData] = useState<VerifyCodeResponse | null>(null);
const [joinResult, setJoinResult] = useState<JoinWaitlistResponse | null>(null);

// Layout: centered content, max-w-sm, no app shell, inline wordmark
// Mobile-first: full viewport height, centered vertically
```

Layout structure:
```
<div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
  <div className="w-full max-w-sm">
    {/* Shadowbrook wordmark at top */}
    <h1 className="text-xl font-bold text-center mb-8">Shadowbrook</h1>

    {phase === 'code' && <CodeEntry onVerified={handleVerified} />}
    {phase === 'join' && <JoinForm verifyData={verifyData!} onJoined={handleJoined} />}
    {phase === 'confirmation' && <Confirmation result={joinResult!} />}
  </div>
</div>
```

### 4.7 Component: CodeEntry

**Create:** `src/web/src/features/walkup/components/CodeEntry.tsx`

Props:
```typescript
interface CodeEntryProps {
  onVerified: (data: VerifyCodeResponse) => void;
}
```

Implementation:
- Single `<Input>` with `inputMode="numeric"`, `pattern="[0-9]*"`, `maxLength={4}`, `autoFocus`
- `placeholder="0000"`, large centered text styling: `text-center text-3xl font-mono tracking-widest`
- Auto-submit on 4th digit via `onChange` handler:
  ```
  if (value.length === 4 && /^\d{4}$/.test(value)) {
    verifyMutation.mutate(value);
  }
  ```
- Show loading spinner/state while verifying
- Error display below input:
  - 404: "Code not found. Check the code posted at the course and try again."
  - Generic: "Something went wrong. Please try again."
- On success, call `onVerified(data)`
- Helper text below input: "Enter the 4-digit code posted at the course"

### 4.8 Component: JoinForm

**Create:** `src/web/src/features/walkup/components/JoinForm.tsx`

Props:
```typescript
interface JoinFormProps {
  verifyData: VerifyCodeResponse;
  onJoined: (result: JoinWaitlistResponse) => void;
}
```

Implementation:
- Heading: `<h2>{verifyData.courseName}</h2>`
- React Hook Form + Zod schema:
  ```typescript
  const joinSchema = z.object({
    firstName: z.string().min(1, 'First name is required'),
    lastName: z.string().min(1, 'Last name is required'),
    phone: z.string().min(10, 'Enter a valid phone number'),
  });
  ```
- Three fields: First Name, Last Name, Phone (`inputMode="tel"`, `type="tel"`)
- Submit button: `<Button size="lg" className="w-full">Join Waitlist</Button>`
- Loading state: button disabled, text "Joining..."
- Error handling:
  - 409 (duplicate): Show existing position as success-like state -- transition to confirmation with the position from the error response
  - 400: Show inline validation errors
  - Generic: "Something went wrong. Please try again."
- On success, call `onJoined(result)`

### 4.9 Component: Confirmation

**Create:** `src/web/src/features/walkup/components/Confirmation.tsx`

Props:
```typescript
interface ConfirmationProps {
  result: JoinWaitlistResponse;
}
```

Implementation:
- Green check icon (use a simple SVG circle with checkmark, or a text character)
- Heading: `You're on the list, {firstName}!`
  - Extract first name from `result.golferName` (split on space, take first)
- Subheading: `#{result.position} in line at {result.courseName}`
- Instruction text: "Keep your phone handy -- we'll text you when a spot opens up."
- No navigation buttons (dead end by design -- golfer waits for SMS)

### 4.10 Feature Entry Point

**Create:** `src/web/src/features/walkup/index.tsx`

```typescript
import { Routes, Route } from 'react-router';
import WalkupJoinPage from './pages/WalkupJoinPage';

export default function WalkupFeature() {
  return (
    <Routes>
      <Route index element={<WalkupJoinPage />} />
    </Routes>
  );
}
```

---

## 5. Testing Strategy

### 5.1 Backend Unit Tests: Phone Normalization

**Create:** `tests/api/PhoneNormalizerTests.cs`

Test cases:
| Input | Expected Output |
|-------|----------------|
| `"5551234567"` | `"+15551234567"` |
| `"15551234567"` | `"+15551234567"` |
| `"+15551234567"` | `"+15551234567"` |
| `"(555) 123-4567"` | `"+15551234567"` |
| `"555-123-4567"` | `"+15551234567"` |
| `"555.123.4567"` | `"+15551234567"` |
| `"+1 (555) 123-4567"` | `"+15551234567"` |
| `"123"` | `null` (too short) |
| `""` | `null` |
| `null` | `null` |
| `"12345678901234"` | `null` (too long) |
| `"abcdefghij"` | `null` (non-numeric) |

### 5.2 Backend Integration Tests: Walkup Join Endpoints

**Create:** `tests/api/WalkupJoinEndpointsTests.cs`

Pattern: follows existing `WalkUpWaitlistEndpointsTests.cs` conventions (IClassFixture, helper methods, inline record types).

**Test cases for POST /walkup/verify:**

1. `Verify_ValidActiveCode_ReturnsCourseName` -- open waitlist, verify its code, get 200
2. `Verify_InvalidCode_Returns404` -- no matching code today
3. `Verify_ClosedWaitlistCode_Returns404` -- code exists but status is Closed
4. `Verify_MalformedCode_Returns400` -- "abc", "12345", ""
5. `Verify_CodeFromYesterday_Returns404` -- (hard to test with fixed clock, may skip)

**Test cases for POST /walkup/join:**

6. `Join_ValidRequest_Returns201_WithPosition` -- full happy path
7. `Join_CreatesGolferRecord` -- verify Golfer entity created
8. `Join_ExistingGolfer_ReusesGolferRecord` -- same phone, different waitlist
9. `Join_DuplicatePhone_Returns409_WithPosition` -- same phone, same waitlist
10. `Join_InvalidPhone_Returns400` -- "123"
11. `Join_MissingFields_Returns400` -- empty first name, etc.
12. `Join_ClosedWaitlist_Returns404` -- waitlist closed between verify and join
13. `Join_NonexistentWaitlist_Returns404`
14. `Join_MultipleGolfers_PositionsAreCorrect` -- join 3 golfers, verify positions 1, 2, 3
15. `Join_SendsSmsConfirmation` -- verify ITextMessageService.SendAsync called (requires test spy -- see note)

**Note on SMS testing:** The `TestWebApplicationFactory` uses `ConsoleTextMessageService` which logs to console. For verifying SMS was sent, either:
- Register a mock `ITextMessageService` in the test factory
- Or accept that the integration test verifies the endpoint response and trust the event handler unit test separately

**Helper method pattern:**
```csharp
private async Task<(Guid TenantId, Guid CourseId, string ShortCode)> CreateOpenWaitlistAsync()
{
    // Create tenant, create course, open walkup waitlist, return IDs + code
}
```

### 5.3 Backend Integration: Operator View Shows Entries

**Modify:** `tests/api/WalkUpWaitlistEndpointsTests.cs`

Add test:
- `Today_WithGolferEntries_ReturnsEntriesInQueue` -- open waitlist, join via /walkup/join, then verify GET /walkup-waitlist/today returns the entry

### 5.4 Frontend Component Tests

**Create:** `src/web/src/features/walkup/__tests__/WalkupJoinPage.test.tsx`

Tests:
1. Renders code entry phase initially
2. Transitions to join form after successful code verification
3. Transitions to confirmation after successful join
4. Shows Shadowbrook wordmark

**Create:** `src/web/src/features/walkup/__tests__/CodeEntry.test.tsx`

Tests:
1. Renders input with numeric mode
2. Auto-submits on 4th digit
3. Shows error on 404 response
4. Shows loading state during verification
5. Does not submit with fewer than 4 digits
6. Strips non-numeric characters

**Create:** `src/web/src/features/walkup/__tests__/JoinForm.test.tsx`

Tests:
1. Renders course name as heading
2. Shows validation errors for empty fields
3. Shows validation error for short phone
4. Disables button during submission
5. Calls onJoined on success

**Create:** `src/web/src/features/walkup/__tests__/Confirmation.test.tsx`

Tests:
1. Shows golfer first name in heading
2. Shows position number
3. Shows course name
4. Shows "keep your phone handy" text

### 5.5 Testing Pattern Notes

- Frontend tests mock the hooks (`vi.mock('../hooks/useWalkupJoin')`) following the pattern in `WalkUpWaitlist.test.tsx`
- Backend tests use `TestWebApplicationFactory` with SQLite in-memory, following the pattern in `WalkUpWaitlistEndpointsTests.cs`
- No X-Tenant-Id header needed for `/walkup/*` requests in tests

---

## 6. Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `src/api/Models/Golfer.cs` | Golfer entity (phone-keyed identity stub) |
| `src/api/Models/GolferWaitlistEntry.cs` | Waitlist entry entity with required GolferId FK |
| `src/api/Services/PhoneNormalizer.cs` | E.164 phone normalization utility |
| `src/api/Endpoints/WalkupJoinEndpoints.cs` | Public /walkup/verify and /walkup/join endpoints |
| `src/api/Events/GolferJoinedWaitlist.cs` | Domain event record |
| `src/api/Events/GolferJoinedWaitlistSmsHandler.cs` | SMS confirmation handler |
| `src/api/Migrations/<timestamp>_AddGolferAndWaitlistEntries.cs` | EF migration |
| `tests/api/PhoneNormalizerTests.cs` | Phone normalization unit tests |
| `tests/api/WalkupJoinEndpointsTests.cs` | Integration tests for public endpoints |
| `src/web/src/features/walkup/index.tsx` | Feature entry point |
| `src/web/src/features/walkup/pages/WalkupJoinPage.tsx` | Three-phase state machine page |
| `src/web/src/features/walkup/components/CodeEntry.tsx` | Phase 1 component |
| `src/web/src/features/walkup/components/JoinForm.tsx` | Phase 2 component |
| `src/web/src/features/walkup/components/Confirmation.tsx` | Phase 3 component |
| `src/web/src/features/walkup/hooks/useWalkupJoin.ts` | TanStack Query mutations |
| `src/web/src/features/walkup/__tests__/WalkupJoinPage.test.tsx` | Page integration tests |
| `src/web/src/features/walkup/__tests__/CodeEntry.test.tsx` | Code entry tests |
| `src/web/src/features/walkup/__tests__/JoinForm.test.tsx` | Join form tests |
| `src/web/src/features/walkup/__tests__/Confirmation.test.tsx` | Confirmation tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/api/Data/ApplicationDbContext.cs` | Add DbSet for Golfer and GolferWaitlistEntry; add EF configuration in OnModelCreating |
| `src/api/Models/CourseWaitlist.cs` | Add `GolferWaitlistEntries` navigation collection |
| `src/api/Endpoints/WalkUpWaitlistEndpoints.cs` | Update `GetToday` to return real entries from GolferWaitlistEntries |
| `src/api/Program.cs` | Register SMS event handler; map WalkupJoinEndpoints |
| `src/web/src/app/router.tsx` | Add `/join` public route (no AuthGuard) |
| `src/web/src/lib/query-keys.ts` | Add `walkupJoin` key factory |
| `src/web/src/types/waitlist.ts` | Add VerifyCodeResponse, JoinWaitlistRequest, JoinWaitlistResponse types |
| `tests/api/WalkUpWaitlistEndpointsTests.cs` | Add test for entries appearing in GET /today after join |
| `docs/plans/waitlist-architecture.md` | Update Section 2.1: GolferId is required (non-nullable) on GolferWaitlistEntry; remove "nullable interim state" language |

---

## 7. Dev Task Checklist

### Backend Developer

- [ ] **B1.** Create `src/api/Models/Golfer.cs` entity
- [ ] **B2.** Create `src/api/Models/GolferWaitlistEntry.cs` entity
- [ ] **B3.** Update `src/api/Models/CourseWaitlist.cs` with navigation property
- [ ] **B4.** Update `src/api/Data/ApplicationDbContext.cs` with DbSets and EF configuration
- [ ] **B5.** Generate and verify EF migration: `AddGolferAndWaitlistEntries`
- [ ] **B6.** Create `src/api/Services/PhoneNormalizer.cs`
- [ ] **B7.** Create `tests/api/PhoneNormalizerTests.cs` and verify all pass
- [ ] **B8.** Create `src/api/Events/GolferJoinedWaitlist.cs` domain event
- [ ] **B9.** Create `src/api/Events/GolferJoinedWaitlistSmsHandler.cs`
- [ ] **B10.** Create `src/api/Endpoints/WalkupJoinEndpoints.cs` with POST /walkup/verify and POST /walkup/join
- [ ] **B11.** Update `src/api/Program.cs` -- register handler and map endpoints
- [ ] **B12.** Update `src/api/Endpoints/WalkUpWaitlistEndpoints.cs` -- `GetToday` returns real entries
- [ ] **B13.** Create `tests/api/WalkupJoinEndpointsTests.cs` with all test cases
- [ ] **B14.** Update `tests/api/WalkUpWaitlistEndpointsTests.cs` with entry visibility test
- [ ] **B15.** Run `dotnet build shadowbrook.slnx` -- verify compilation
- [ ] **B16.** Run `dotnet test` -- verify all tests pass
- [ ] **B17.** Update `docs/plans/waitlist-architecture.md` Section 2.1 -- GolferId required

### Frontend Developer

- [ ] **F1.** Add types to `src/web/src/types/waitlist.ts`
- [ ] **F2.** Add query keys to `src/web/src/lib/query-keys.ts`
- [ ] **F3.** Create `src/web/src/features/walkup/hooks/useWalkupJoin.ts`
- [ ] **F4.** Create `src/web/src/features/walkup/components/CodeEntry.tsx`
- [ ] **F5.** Create `src/web/src/features/walkup/components/JoinForm.tsx`
- [ ] **F6.** Create `src/web/src/features/walkup/components/Confirmation.tsx`
- [ ] **F7.** Create `src/web/src/features/walkup/pages/WalkupJoinPage.tsx`
- [ ] **F8.** Create `src/web/src/features/walkup/index.tsx`
- [ ] **F9.** Update `src/web/src/app/router.tsx` -- add `/join` route
- [ ] **F10.** Create `src/web/src/features/walkup/__tests__/CodeEntry.test.tsx`
- [ ] **F11.** Create `src/web/src/features/walkup/__tests__/JoinForm.test.tsx`
- [ ] **F12.** Create `src/web/src/features/walkup/__tests__/Confirmation.test.tsx`
- [ ] **F13.** Create `src/web/src/features/walkup/__tests__/WalkupJoinPage.test.tsx`
- [ ] **F14.** Run `pnpm --dir src/web lint` -- verify no lint errors
- [ ] **F15.** Run `pnpm --dir src/web test` -- verify all tests pass

---

## 8. Risks and Edge Cases

### 8.1 Golfer Unique Index Race Condition

Two concurrent requests with the same phone number could both try to create a Golfer record. The unique index on `Phone` will cause one to fail with `DbUpdateException`. The endpoint must catch this and re-query for the existing golfer. This is a standard lookup-or-create pattern.

### 8.2 Waitlist Closes Between Verify and Join

The golfer verifies the code (waitlist is open), then the operator closes the waitlist before the golfer submits. The join endpoint must re-check `Status == "Open"` and return 404 with a clear message: "This waitlist is no longer accepting new entries."

### 8.3 Query Filter Bypass

The `/walkup/*` endpoints use `.IgnoreQueryFilters()` because there is no tenant context. This is intentional and safe for this use case. The `Golfer` entity has no tenant scope. The `CourseWaitlist` lookup is scoped by short code + date, which is not tenant-sensitive data.

### 8.4 Phone Number Formatting UX

Mobile browsers with `inputMode="tel"` and `type="tel"` will show a phone keypad. The backend normalizes whatever format the user enters (with dashes, parens, spaces, etc.) to E.164. The frontend should NOT enforce strict formatting -- let the user type naturally.

### 8.5 API Client Tenant Header

The existing `api-client.ts` attaches `X-Tenant-Id` to every request if `activeTenantId` is set. For the `/walkup/*` endpoints, this header is harmless (the middleware reads it but the endpoints use `.IgnoreQueryFilters()`). No change needed to the API client.

### 8.6 SMS Failure

Per project principles, downstream failures (SMS) must not break the core flow. The `InProcessDomainEventPublisher` already catches and logs handler exceptions. If SMS fails, the golfer still joins the waitlist successfully.

---

## 9. Architecture Doc Update

**File:** `docs/plans/waitlist-architecture.md`

Update Section 2.1 `GolferWaitlistEntry` definition:
- Change `GolferId Guid?, FK -> Golfer, nullable` to `GolferId Guid, FK -> Golfer, required`
- Remove paragraph about "interim state before the Golfer entity exists" (Section 2.1 point 2)
- Update the phased table creation table in Section 2.5 to show `Golfers` and `GolferWaitlistEntries` are created in story #31
- Update Section 7.1 (Golfer Entity Timing) to note that the Golfer entity is introduced in #31 with the walk-up join flow
