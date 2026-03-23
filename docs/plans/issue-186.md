# Implementation Plan for #186 -- QR Code for Walk-Up Waitlist

## Approach

Add a new backend endpoint that returns the QR code URL for an active waitlist, create a new public `/w/:shortCode` landing page that resolves a short code and shows waitlist status (open/closed/expired), and embed a QR code panel in the existing operator `WalkUpWaitlist` page. QR generation is entirely client-side using the `qrcode.react` library. Download and print are browser-native operations (canvas-to-PNG and `window.print()`).

## Key Observation: Existing Infrastructure Is Already Sufficient

After reviewing the codebase, the `WalkUpWaitlist` entity already has `ShortCode`, `Status`, `ClosedAt`, and `Date`. The `WalkUpJoinEndpoints.VerifyShortCode` endpoint already resolves short codes and differentiates between not-found, closed (410), and date-mismatched cases. The `WalkUpWaitlistEndpoints.GetToday` response already includes `shortCode`. This means:

- **No database migration is needed** -- the data model already supports everything
- **No new backend endpoint is strictly required for QR generation** -- the short code is already returned in the `GET /courses/{courseId}/walkup-waitlist/today` response
- **One new backend endpoint IS needed** -- a public `GET /walkup/status/{shortCode}` that returns the waitlist state for the golfer landing page (the existing `POST /walkup/verify` is close but uses POST and doesn't return the right shape for the landing page)

## Files

### Backend

- **Create:** `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpQrEndpoints.cs` -- public GET endpoint to resolve short code status for the golfer landing page
- **Modify:** (none -- existing endpoints already return everything operators need)

### Frontend

- **Create:** `src/web/src/features/walkup-qr/index.tsx` -- feature route for `/w/*`
- **Create:** `src/web/src/features/walkup-qr/pages/WalkUpLandingPage.tsx` -- public golfer landing page (open/closed/expired states)
- **Create:** `src/web/src/features/walkup-qr/hooks/useWalkUpStatus.ts` -- TanStack Query hook for the status endpoint
- **Create:** `src/web/src/features/operator/components/QrCodePanel.tsx` -- QR code card for operator dashboard
- **Modify:** `src/web/src/features/operator/pages/WalkUpWaitlist.tsx` -- embed QrCodePanel in the active-waitlist view
- **Modify:** `src/web/src/app/router.tsx` -- add `/w/*` public route
- **Modify:** `src/web/package.json` -- add `qrcode.react` dependency

### Tests

- **Create:** `tests/Shadowbrook.Api.Tests/WalkUpQrEndpointsTests.cs` -- integration tests for the status endpoint
- **Create:** `src/web/src/features/operator/__tests__/QrCodePanel.test.tsx` -- unit tests for the QR panel component
- **Create:** `src/web/src/features/walkup-qr/__tests__/WalkUpLandingPage.test.tsx` -- unit tests for the landing page states

## Patterns

### Backend Endpoint Pattern

Follow `WalkUpJoinEndpoints.cs` exactly -- Wolverine HTTP attribute-based static methods, `IgnoreQueryFilters()` for cross-tenant access, inline record DTOs at bottom of file. Reference:

```
// Existing pattern in WalkUpJoinEndpoints.cs:
[WolverinePost("/walkup/verify")]
public static async Task<IResult> VerifyShortCode(VerifyCodeRequest request, ApplicationDbContext db, TimeProvider timeProvider)
```

### Public Route Pattern

The `/w/*` route follows the same pattern as `/join/*` and `/book/walkup/*` in `router.tsx` -- lazy-loaded feature outside `AuthGuard`. Reference: `router.tsx` lines 66-76.

### QR Code Pattern

Use `qrcode.react` (`QRCodeCanvas` component) for rendering. The QR encodes the full URL `{window.location.origin}/w/{shortCode}`. Download uses `canvas.toDataURL('image/png')` + programmatic anchor click. Print uses CSS `@media print` rules to isolate the QR content.

### Component Pattern

`QrCodePanel` is a self-contained `Card` component that receives `shortCode` as a prop. It follows the operator component pattern from `OpenWaitlistDialog.tsx` / `CloseWaitlistDialog.tsx` -- a focused, single-purpose component in `features/operator/components/`.

## API Design

### `GET /walkup/status/{shortCode}` (Public -- no auth, no tenant header)

Resolves a short code and returns the current waitlist state for the golfer landing page.

**Response scenarios:**

1. **Open waitlist (today's date):**
   ```
   200 OK
   {
     "status": "open",
     "courseName": "Pine Valley Golf Club",
     "date": "2026-03-23"
   }
   ```

2. **Closed waitlist (today's date):**
   ```
   200 OK
   {
     "status": "closed",
     "courseName": "Pine Valley Golf Club",
     "date": "2026-03-23"
   }
   ```

3. **Expired (different date -- QR from a prior day):**
   ```
   200 OK
   {
     "status": "expired",
     "courseName": "Pine Valley Golf Club",
     "date": "2026-03-22"
   }
   ```

4. **Not found (invalid code):**
   ```
   404 Not Found
   { "error": "This QR code is not valid." }
   ```

**Why return 200 for closed/expired instead of 410/404?** The golfer landing page needs to render different friendly UI states (closed message vs expired message). Returning 200 with a `status` discriminator is cleaner than parsing error responses. The existing `POST /walkup/verify` uses 410 for closed, but that endpoint serves a different purpose (the join flow needs to block progression).

**Why a new endpoint instead of reusing `POST /walkup/verify`?** Three reasons:
1. `POST /walkup/verify` uses POST (inappropriate for an idempotent status check from a QR scan)
2. It returns `courseWaitlistId` (not needed for landing page display)
3. It returns 410 for closed and 404 for expired -- the landing page needs to differentiate these with friendly UI, not treat them as errors

## Data Model

No changes needed. The existing `WalkUpWaitlist` entity has all required fields:
- `ShortCode` (string) -- already generated when waitlist opens
- `Status` (enum: Open, Closed) -- already tracked
- `Date` (DateOnly) -- used for expiry detection
- `ClosedAt` (DateTimeOffset?) -- already set on close

## Frontend Component Details

### QrCodePanel (`features/operator/components/QrCodePanel.tsx`)

Props: `shortCode: string`

Renders inside a `Card` component:
- `QRCodeCanvas` at 240px (desktop) / 200px (mobile via responsive class)
- Short URL text below QR in monospace: `{origin}/w/{shortCode}`
- Two action buttons: "Download PNG" and "Print"
- `aria-label="QR code for walk-up waitlist"` on the canvas wrapper

**Download implementation:**
```
// Pseudocode
const canvas = document.querySelector('#qr-canvas canvas');
const url = canvas.toDataURL('image/png');
const a = document.createElement('a');
a.href = url;
a.download = `waitlist-${shortCode}.png`;
a.click();
```

**Print implementation:**
```
// Pseudocode
window.print();
// CSS @media print hides everything except the QR panel content
```

**Print CSS** (add to a `<style>` block or a CSS file):
```css
@media print {
  body * { visibility: hidden; }
  #qr-print-area, #qr-print-area * { visibility: visible; }
  #qr-print-area { position: absolute; top: 0; left: 0; text-align: center; }
}
```

### WalkUpLandingPage (`features/walkup-qr/pages/WalkUpLandingPage.tsx`)

Route: `/w/:shortCode`

States:
- **Loading:** Skeleton card, centered
- **Open:** Card with course name, date, and message "The walk-up waitlist is open. Ask staff or enter your code at {origin}/join/{shortCode}" with a link/button to the existing join flow
- **Closed:** Card with Clock icon (lucide-react) + "The walk-up waitlist is closed for today. No new entries are being accepted."
- **Expired:** Card with CalendarX2 icon (lucide-react) + "This QR code is no longer valid. Ask for today's code at the pro shop."
- **Not found (404):** Same as expired -- "This QR code is not valid."
- **Network error:** "Something went wrong." + "Try again" button

Layout: `min-h-dvh flex items-center justify-center`, card at `max-w-md`, matching the existing `WalkupJoinPage` visual pattern.

**Key UX decision for "open" state:** The QR landing page should redirect/link to the existing join flow at `/join/{shortCode}`. The `CodeEntry` component in the walkup feature already handles auto-verification when `shortCode` is in the URL params. So the QR landing page for "open" state can simply redirect to `/join/{shortCode}`, which triggers the existing verify -> join flow seamlessly.

### Integration into WalkUpWaitlist Page

In the active (Open) state section of `WalkUpWaitlist.tsx`, add `<QrCodePanel shortCode={waitlist.shortCode} />` between the `PageHeader` and the `Tabs` component. This places the QR code prominently when the waitlist is open.

In the closed state, do NOT show the QR panel (the QR code is not useful once closed).

## Risks

1. **`qrcode.react` compatibility with React 19:** This is a widely-used library; latest versions support React 19. Verify during install.

2. **Print CSS isolation:** The `@media print` approach using visibility toggling can be fragile. An alternative is `window.open()` with a print-specific page, but the visibility approach is simpler and sufficient for v1.

3. **QR URL stability:** The QR encodes `{origin}/w/{shortCode}`. The `origin` comes from `window.location.origin` at render time. In development this is `localhost:3000`, in production it's the real domain. No env variable needed.

4. **Short code is 4 digits:** The existing `ShortCodeGenerator` produces 4-digit numeric codes. These are unique per date but NOT globally unique. The QR landing page endpoint must validate the date, not just the code existence. The implementation uses `TimeProvider` + course timezone to determine "today" -- matching the existing pattern in `WalkUpJoinEndpoints.VerifyShortCode`.

## Testing Strategy

### Backend Integration Tests (`WalkUpQrEndpointsTests.cs`)

| Test | Scenario |
|------|----------|
| `Status_OpenWaitlist_ReturnsOpen` | Create tenant + course, open waitlist, GET status -- returns "open" with course name |
| `Status_ClosedWaitlist_ReturnsClosed` | Open then close waitlist, GET status -- returns "closed" |
| `Status_InvalidCode_Returns404` | GET status with bogus code -- returns 404 |
| `Status_NoTenantHeader_StillWorks` | Ensure the public endpoint works without X-Tenant-Id header |

Note: Testing the "expired" scenario (different date) is hard in integration tests because `TimeProvider` is shared. This is acceptable as a known gap -- the logic is straightforward (compare `waitlist.Date != today`).

### Frontend Unit Tests (`QrCodePanel.test.tsx`)

| Test | Scenario |
|------|----------|
| Renders QR code canvas | Given shortCode, QRCodeCanvas is rendered |
| Displays short URL | Shows `{origin}/w/{shortCode}` text |
| Download button triggers canvas export | Mock canvas.toDataURL, verify anchor click |
| Print button calls window.print | Mock window.print, verify call |

### Frontend Unit Tests (`WalkUpLandingPage.test.tsx`)

| Test | Scenario |
|------|----------|
| Loading state shows skeleton | Mock hook as loading, verify skeleton |
| Open state redirects to join page | Mock status "open", verify redirect/link to `/join/{shortCode}` |
| Closed state shows closed message | Mock status "closed", verify message text |
| Expired state shows expired message | Mock status "expired", verify message text |
| 404 state shows invalid message | Mock hook with 404 error, verify message |
| Error state shows retry button | Mock hook with network error, verify retry button |

### Existing Test Updates

The existing `WalkUpWaitlist.test.tsx` should get one new test:
- `shows QR code panel when waitlist is open` -- verify QrCodePanel renders with the short code

## Dev Tasks

### Backend Developer

- [ ] Create `src/backend/Shadowbrook.Api/Features/WalkUpWaitlist/WalkUpQrEndpoints.cs` with `GET /walkup/status/{shortCode}` endpoint:
  - Static method with `[WolverineGet("/walkup/status/{shortCode}")]`
  - Inject `ApplicationDbContext` and `TimeProvider`
  - Query `db.WalkUpWaitlists.IgnoreQueryFilters()` to find waitlist by short code
  - If not found, return 404 with error message
  - Join to `db.Courses.IgnoreQueryFilters()` to get course name and timezone
  - Compute `today` using `CourseTime.Today(timeProvider, timeZoneId)`
  - Return status: "open" if `Status == Open && Date == today`, "closed" if `Status == Closed && Date == today`, "expired" if `Date != today`
  - Define response record: `WalkUpQrStatusResponse(string Status, string CourseName, string Date)`
- [ ] Create `tests/Shadowbrook.Api.Tests/WalkUpQrEndpointsTests.cs` with integration tests covering open, closed, invalid code, and no-tenant-header scenarios (follow `WalkUpJoinEndpointsTests` pattern exactly -- `[Collection("Integration")]`, `TestWebApplicationFactory`, `ResetDatabaseAsync`)
- [ ] Verify build compiles: `dotnet build shadowbrook.slnx`
- [ ] Run `dotnet format shadowbrook.slnx` to fix any style issues
- [ ] Run the new integration tests to verify they pass

### Frontend Developer

- [ ] Install `qrcode.react`: `pnpm --dir src/web add qrcode.react`
- [ ] Create `src/web/src/features/operator/components/QrCodePanel.tsx`:
  - Import `QRCodeCanvas` from `qrcode.react`
  - Accept `shortCode: string` prop
  - Render inside a shadcn `Card` with `CardContent`
  - QR canvas renders `${window.location.origin}/w/${shortCode}` at size 240 (desktop) / 200 (mobile)
  - Display the URL in monospace text below the QR
  - "Download PNG" button: get canvas element, call `toDataURL('image/png')`, trigger download via programmatic anchor
  - "Print" button: call `window.print()`
  - Add a `div` with `id="qr-print-area"` wrapping the printable content
  - Add `@media print` CSS (inline `<style>` tag or in the component) to isolate QR content during printing
  - `aria-label="QR code for walk-up waitlist"` on the canvas wrapper div
- [ ] Modify `src/web/src/features/operator/pages/WalkUpWaitlist.tsx`:
  - Import `QrCodePanel`
  - In the active (Open) state section, add `<QrCodePanel shortCode={waitlist.shortCode} />` between the `PageHeader` block and the `Tabs` block (around line 335, after the dialog components)
  - Do NOT add QrCodePanel to the closed state or inactive state
- [ ] Add query key to `src/web/src/lib/query-keys.ts`:
  - Add `walkUpQr: { status: (shortCode: string) => ['walkup-qr', 'status', shortCode] as const }`
- [ ] Create `src/web/src/features/walkup-qr/hooks/useWalkUpStatus.ts`:
  - `useQuery` hook calling `GET /walkup/status/${shortCode}` via `api.get`
  - Use `queryKeys.walkUpQr.status(shortCode)` as query key
  - `enabled: !!shortCode`
  - Define `WalkUpQrStatus` type: `{ status: 'open' | 'closed' | 'expired'; courseName: string; date: string }`
- [ ] Create `src/web/src/features/walkup-qr/pages/WalkUpLandingPage.tsx`:
  - Read `shortCode` from URL params via `useParams`
  - Call `useWalkUpStatus(shortCode)`
  - Render centered card layout matching existing `WalkupJoinPage` visual style (`min-h-dvh flex items-center justify-center`)
  - **Loading:** Skeleton inside card
  - **Open:** Show course name + redirect link to `/join/${shortCode}` (the existing join flow handles auto-verify)
  - **Closed:** Clock icon from `lucide-react` + "The walk-up waitlist is closed for today." message
  - **Expired:** CalendarX2 icon from `lucide-react` + "This QR code is no longer valid." message
  - **404 error:** Same as expired display
  - **Network error:** "Something went wrong." + "Try Again" button that calls `refetch()`
  - Set `document.title` dynamically based on state (e.g., "Walk-Up Waitlist - Pine Valley")
  - Use `sr-only` text for loading state, `aria-hidden` on decorative icons
- [ ] Create `src/web/src/features/walkup-qr/index.tsx`:
  - Feature router with `<Routes><Route path=":shortCode" element={<WalkUpLandingPage />} /></Routes>`
- [ ] Modify `src/web/src/app/router.tsx`:
  - Add lazy import: `const WalkUpQrFeature = lazy(() => import('@/features/walkup-qr'));`
  - Add route entry (outside AuthGuard, alongside `/join/*` and `/book/walkup/*`):
    ```
    { path: '/w/*', element: <LazyFeature><WalkUpQrFeature /></LazyFeature> }
    ```
- [ ] Create `src/web/src/features/operator/__tests__/QrCodePanel.test.tsx`:
  - Test that QR canvas renders with correct value
  - Test that short URL text is displayed
  - Test download button functionality (mock canvas `toDataURL`)
  - Test print button calls `window.print()`
- [ ] Create `src/web/src/features/walkup-qr/__tests__/WalkUpLandingPage.test.tsx`:
  - Mock `useWalkUpStatus` hook
  - Test loading skeleton state
  - Test open state shows course name and link to join
  - Test closed state shows closed message with Clock icon
  - Test expired state shows expired message with CalendarX2 icon
  - Test error state shows retry button
- [ ] Update `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`:
  - Add test: "shows QR code panel when waitlist is open" -- verify QrCodePanel renders (mock `qrcode.react`)
  - Add mock for `QrCodePanel` component (since it uses canvas, mock the whole component in the parent test)
- [ ] Run `pnpm --dir src/web lint` to verify no lint errors
- [ ] Run `pnpm --dir src/web test` to verify all tests pass
