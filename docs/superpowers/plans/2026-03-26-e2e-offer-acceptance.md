# E2E Offer Acceptance + Domain Naming Alignment

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename UI/API to match the `TeeTimeOpening` domain aggregate, then extend e2e tests to cover the full offer acceptance flow (operator adds opening → golfer receives SMS → golfer accepts offer).

**Architecture:** The rename is a straightforward find-and-replace across backend endpoint, frontend hook, type, and test files. The e2e extension adds three serial tests to the existing walkup flow, using the dev SMS API to poll for offer messages and extract the token URL.

**Tech Stack:** .NET 10 (Wolverine HTTP), React 19 + TypeScript, Playwright e2e, dev SMS API

---

## File Map

### Rename (Task 1-2)

**Backend:**
- Modify: `src/backend/Shadowbrook.Api/Features/Waitlist/Endpoints/WalkUpWaitlistEndpoints.cs` — rename record, validator, route
- Modify: `tests/Shadowbrook.Api.Tests/Validators/CreateWalkUpWaitlistRequestRequestValidatorTests.cs` — update record/validator references

**Frontend:**
- Rename: `src/web/src/features/operator/components/AddTeeTimeRequestDialog.tsx` → `AddTeeTimeOpeningDialog.tsx`
- Rename: `src/web/src/features/operator/__tests__/AddTeeTimeRequestDialog.test.tsx` → `AddTeeTimeOpeningDialog.test.tsx`
- Modify: `src/web/src/features/operator/pages/WalkUpWaitlist.tsx` — update import path
- Modify: `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx` — update test descriptions
- Modify: `src/web/src/features/operator/hooks/useWaitlist.ts` — update API URL
- Modify: `src/web/src/types/waitlist.ts` — rename type

### E2E Tests (Task 3-5)

- Modify: `src/web/e2e/playwright.config.ts` — add `E2E_API_URL`
- Modify: `src/web/e2e/fixtures/operator-waitlist-page.ts` — add `addTeeTimeOpening()` method
- Modify: `src/web/e2e/fixtures/test-data.ts` — add `TEST_API_URL` and SMS helper
- Create: `src/web/e2e/fixtures/sms-helper.ts` — SMS polling utility
- Create: `src/web/e2e/fixtures/walk-up-offer-page.ts` — offer acceptance page object
- Modify: `src/web/e2e/tests/walkup/walkup-flow.spec.ts` — add 3 new tests

---

### Task 1: Backend — Rename endpoint and records

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Waitlist/Endpoints/WalkUpWaitlistEndpoints.cs`
- Modify: `tests/Shadowbrook.Api.Tests/Validators/CreateWalkUpWaitlistRequestRequestValidatorTests.cs`

- [ ] **Step 1: Rename the request record, validator, and route**

In `WalkUpWaitlistEndpoints.cs`, make these changes:

1. Change the route from `/courses/{courseId}/walkup-waitlist/openings` to `/courses/{courseId}/tee-time-openings`:

```csharp
[WolverinePost("/courses/{courseId}/tee-time-openings")]
public static async Task<IResult> CreateOpening(
    Guid courseId,
    CreateTeeTimeOpeningRequest request,
    ApplicationDbContext db,
    ITeeTimeOpeningRepository openingRepo,
    ITimeProvider timeProvider,
    TimeProvider systemTimeProvider)
{
    var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
    var today = CourseTime.Today(systemTimeProvider, timeZoneId);
    var parsedTeeTime = TimeOnly.ParseExact(request.TeeTime, ["HH:mm", "HH:mm:ss"]);

    var opening = TeeTimeOpening.Create(courseId, today, parsedTeeTime, request.SlotsAvailable, operatorOwned: true, timeProvider);
    openingRepo.Add(opening);

    return Results.Created(
        $"/courses/{courseId}/tee-time-openings/{opening.Id}",
        new WalkUpWaitlistOpeningResponse(opening.Id, opening.TeeTime.Time.ToString("HH:mm"), opening.SlotsAvailable, opening.SlotsRemaining, opening.Status.ToString()));
}
```

2. Rename the record and validator at the bottom of the file:

```csharp
public record CreateTeeTimeOpeningRequest(string TeeTime, int SlotsAvailable);

public class CreateTeeTimeOpeningRequestValidator : AbstractValidator<CreateTeeTimeOpeningRequest>
{
    public CreateTeeTimeOpeningRequestValidator()
    {
        RuleFor(x => x.TeeTime)
            .NotEmpty().WithMessage("Tee time is required.")
            .Must(t => TimeOnly.TryParseExact(t, ["HH:mm", "HH:mm:ss"], out _))
            .WithMessage("A valid tee time in HH:mm format is required.");

        RuleFor(x => x.SlotsAvailable)
            .InclusiveBetween(1, 4)
            .WithMessage("Slots available must be between 1 and 4.");
    }
}
```

- [ ] **Step 2: Update the validator tests**

In `tests/Shadowbrook.Api.Tests/Validators/CreateWalkUpWaitlistRequestRequestValidatorTests.cs`, update all references:

```csharp
using Shadowbrook.Api.Features.Waitlist.Endpoints;

namespace Shadowbrook.Api.Tests.Validators;

public class CreateTeeTimeOpeningRequestValidatorTests
{
    private readonly CreateTeeTimeOpeningRequestValidator validator = new();

    [Fact]
    public void ValidRequest_Passes() =>
        Assert.True(this.validator.Validate(new CreateTeeTimeOpeningRequest("09:00", 2)).IsValid);

    [Fact]
    public void ValidRequest_WithSeconds_Passes() =>
        Assert.True(this.validator.Validate(new CreateTeeTimeOpeningRequest("09:00:00", 2)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("9am")]
    [InlineData("25:00")]
    public void InvalidTeeTime_Fails(string teeTime)
    {
        var result = this.validator.Validate(new CreateTeeTimeOpeningRequest(teeTime, 2));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TeeTime");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public void InvalidSlotsAvailable_Fails(int slots)
    {
        var result = this.validator.Validate(new CreateTeeTimeOpeningRequest("09:00", slots));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SlotsAvailable");
    }
}
```

Also rename the file from `CreateWalkUpWaitlistRequestRequestValidatorTests.cs` to `CreateTeeTimeOpeningRequestValidatorTests.cs`.

- [ ] **Step 3: Build and run validator tests**

Run:
```bash
dotnet build shadowbrook.slnx
dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~CreateTeeTimeOpeningRequestValidatorTests" -v minimal
```

Expected: All 5 validator tests pass.

- [ ] **Step 4: Run dotnet format**

Run: `dotnet format shadowbrook.slnx`

- [ ] **Step 5: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/Waitlist/Endpoints/WalkUpWaitlistEndpoints.cs tests/Shadowbrook.Api.Tests/Validators/
git commit -m "refactor: rename CreateOpeningRequest to CreateTeeTimeOpeningRequest, move endpoint to /tee-time-openings"
```

---

### Task 2: Frontend — Rename dialog, update hook URL, rename type

**Files:**
- Rename: `src/web/src/features/operator/components/AddTeeTimeRequestDialog.tsx` → `AddTeeTimeOpeningDialog.tsx`
- Rename: `src/web/src/features/operator/__tests__/AddTeeTimeRequestDialog.test.tsx` → `AddTeeTimeOpeningDialog.test.tsx`
- Modify: `src/web/src/features/operator/pages/WalkUpWaitlist.tsx`
- Modify: `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`
- Modify: `src/web/src/features/operator/hooks/useWaitlist.ts`
- Modify: `src/web/src/types/waitlist.ts`

- [ ] **Step 1: Rename type in `types/waitlist.ts`**

In `src/web/src/types/waitlist.ts`, rename `CreateWaitlistOpening` to `CreateTeeTimeOpeningRequest`:

```typescript
export interface CreateTeeTimeOpeningRequest {
  teeTime: string;
  slotsAvailable: number;
}
```

- [ ] **Step 2: Update hook URL and type in `useWaitlist.ts`**

In `src/web/src/features/operator/hooks/useWaitlist.ts`, update the import and the API URL:

```typescript
import type {
  AddGolferToWaitlistRequest,
  AddGolferToWaitlistResponse,
  CreateTeeTimeOpeningRequest,
  WaitlistOpeningEntry,
} from '@/types/waitlist';
```

And update the mutation function:

```typescript
export function useCreateTeeTimeOpening() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      courseId,
      data,
    }: {
      courseId: string;
      data: CreateTeeTimeOpeningRequest;
    }) => api.post<WaitlistOpeningEntry>(`/courses/${courseId}/tee-time-openings`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.walkUpWaitlist.today(courseId),
      });
    },
  });
}
```

- [ ] **Step 3: Rename dialog component file**

Rename `src/web/src/features/operator/components/AddTeeTimeRequestDialog.tsx` to `AddTeeTimeOpeningDialog.tsx`.

Update the import inside the file — change the hook import:

```typescript
import { useCreateTeeTimeOpening } from '../hooks/useWaitlist';
```

And update the hook call:

```typescript
const createMutation = useCreateTeeTimeOpening();
```

Also rename the component and its props interface:

```typescript
interface AddTeeTimeOpeningDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  courseId: string;
}

export function AddTeeTimeOpeningDialog({ open, onOpenChange, courseId }: AddTeeTimeOpeningDialogProps) {
```

- [ ] **Step 4: Update the WalkUpWaitlist page import**

In `src/web/src/features/operator/pages/WalkUpWaitlist.tsx`, change:

```typescript
import { AddTeeTimeOpeningDialog } from '../components/AddTeeTimeOpeningDialog';
```

And update the JSX reference:

```tsx
<AddTeeTimeOpeningDialog
  open={addRequestDialogOpen}
  onOpenChange={setAddRequestDialogOpen}
  courseId={courseId}
/>
```

- [ ] **Step 5: Rename test file and update references**

Rename `src/web/src/features/operator/__tests__/AddTeeTimeRequestDialog.test.tsx` to `AddTeeTimeOpeningDialog.test.tsx`.

Update the import and mock references inside the test file:

```typescript
import { AddTeeTimeOpeningDialog } from '../components/AddTeeTimeOpeningDialog';
import { useCreateTeeTimeOpening } from '../hooks/useWaitlist';

vi.mock('../hooks/useWaitlist');

const mockUseCreateTeeTimeOpening = vi.mocked(useCreateTeeTimeOpening);
```

Update the `defaultCreateWaitlistOpening` function:

```typescript
function defaultCreateTeeTimeOpening() {
  mockUseCreateTeeTimeOpening.mockReturnValue({
    mutate: mockCreateMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useCreateTeeTimeOpening>);
}
```

Update `beforeEach`:

```typescript
beforeEach(() => {
  vi.clearAllMocks();
  defaultCourseContext();
  defaultCreateTeeTimeOpening();
});
```

Update the describe block name:

```typescript
describe('AddTeeTimeOpeningDialog', () => {
```

Update all `render` calls:

```tsx
render(
  <AddTeeTimeOpeningDialog
    open={true}
    onOpenChange={vi.fn()}
    courseId="course-1"
  />
);
```

- [ ] **Step 6: Update WalkUpWaitlist test descriptions**

In `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`, update the two test description strings that reference "Add Tee Time Request":

- `'shows Add Tee Time Request button when waitlist is open'` → `'shows Add Tee Time Opening button when waitlist is open'`
- `'does not show Add Tee Time Request button when waitlist is closed'` → `'does not show Add Tee Time Opening button when waitlist is closed'`

Note: The `getByRole` / `queryByRole` calls already query for `'Add Tee Time Opening'` — only the `it()` description strings need updating.

- [ ] **Step 7: Run frontend lint and tests**

Run:
```bash
pnpm --dir src/web lint
pnpm --dir src/web test
```

Expected: All tests pass, no lint errors.

- [ ] **Step 8: Commit**

```bash
git add src/web/src/features/operator/ src/web/src/types/waitlist.ts
git commit -m "refactor: rename AddTeeTimeRequestDialog to AddTeeTimeOpeningDialog, update hook to /tee-time-openings"
```

---

### Task 3: E2E Infrastructure — SMS helper and playwright config

**Files:**
- Modify: `src/web/e2e/playwright.config.ts`
- Create: `src/web/e2e/fixtures/sms-helper.ts`

- [ ] **Step 1: Add `E2E_API_URL` to playwright config**

In `src/web/e2e/playwright.config.ts`, add a custom property via `globalSetup` or just export it. The simplest approach is to use `process.env` directly in the helper. Update the config to document the env var:

```typescript
import { defineConfig } from '@playwright/test';

export const API_BASE_URL = process.env.E2E_API_URL ?? 'https://test-api.shadowbrook.golf';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'https://test.shadowbrook.golf',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' },
    },
  ],
});
```

- [ ] **Step 2: Create SMS polling helper**

Create `src/web/e2e/fixtures/sms-helper.ts`:

```typescript
import { API_BASE_URL } from '../playwright.config';

interface DevSmsMessage {
  id: string;
  from: string;
  to: string;
  body: string;
  direction: string;
  timestamp: string;
}

/**
 * Poll the dev SMS API for a message matching the given predicate.
 * Returns the first matching message body, or throws after timeout.
 */
export async function waitForSms(
  phoneNumber: string,
  predicate: (body: string) => boolean,
  { timeoutMs = 15_000, intervalMs = 500 } = {},
): Promise<string> {
  const deadline = Date.now() + timeoutMs;

  while (Date.now() < deadline) {
    const response = await fetch(`${API_BASE_URL}/dev/sms/conversations/${phoneNumber}`);

    if (response.ok) {
      const messages: DevSmsMessage[] = await response.json();
      const match = messages.find((m) => predicate(m.body));
      if (match) {
        return match.body;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, intervalMs));
  }

  throw new Error(`SMS matching predicate not received within ${timeoutMs}ms for phone ${phoneNumber}`);
}

/**
 * Extract the offer URL path from an SMS body.
 * SMS format: "... Claim your spot: {baseUrl}/book/walkup/{token}"
 * Returns the full path: "/book/walkup/{token}"
 */
export function extractOfferUrl(smsBody: string): string {
  const match = smsBody.match(/\/book\/walkup\/[\w-]+/);
  if (!match) {
    throw new Error(`Could not extract offer URL from SMS: "${smsBody}"`);
  }
  return match[0];
}
```

- [ ] **Step 3: Commit**

```bash
git add src/web/e2e/playwright.config.ts src/web/e2e/fixtures/sms-helper.ts
git commit -m "chore: add E2E_API_URL config and SMS polling helper for e2e tests"
```

---

### Task 4: E2E Page Objects — Extend operator waitlist, create offer page

**Files:**
- Modify: `src/web/e2e/fixtures/operator-waitlist-page.ts`
- Create: `src/web/e2e/fixtures/walk-up-offer-page.ts`

- [ ] **Step 1: Add `addTeeTimeOpening()` to OperatorWaitlistPage**

In `src/web/e2e/fixtures/operator-waitlist-page.ts`, add the method:

```typescript
import { type Page, type Locator } from '@playwright/test';

export class OperatorWaitlistPage {
  private readonly openWaitlistButton: Locator;

  constructor(private readonly page: Page) {
    this.openWaitlistButton = page.getByRole('button', { name: 'Open Waitlist' });
  }

  async goto() {
    await this.page.goto('/operator/waitlist');
  }

  async selectTenant(tenantName: string) {
    await this.page.getByRole('cell', { name: tenantName }).click();
  }

  async selectCourse(courseName: string) {
    await this.page
      .getByRole('button', { name: new RegExp(`Manage ${courseName}`) })
      .click();
    await this.page.getByRole('heading', { name: 'Walk-Up Waitlist' }).waitFor();
  }

  async openWaitlist() {
    await this.openWaitlistButton.click();
    const dialog = this.page.getByRole('alertdialog');
    await dialog.getByRole('button', { name: 'Open Waitlist' }).click();
    await this.page.locator('span.font-mono.font-bold.tracking-widest').waitFor();
  }

  async getShortCode(): Promise<string> {
    const codeElement = this.page.locator('span.font-mono.font-bold.tracking-widest');
    const spacedCode = await codeElement.textContent();
    return spacedCode?.replace(/\s/g, '') ?? '';
  }

  async addTeeTimeOpening(time: string, slots: number) {
    // Click the "Add Tee Time Opening" button to open the dialog
    await this.page.getByRole('button', { name: 'Add Tee Time Opening' }).click();

    // Fill in the tee time
    const dialog = this.page.getByRole('dialog');
    await dialog.getByLabel('Tee Time').fill(time);

    // Select slot count (only change if not default of 1)
    if (slots !== 1) {
      await dialog.getByRole('combobox').click();
      await this.page.getByRole('option', { name: String(slots) }).click();
    }

    // Submit
    await dialog.getByRole('button', { name: 'Add Opening' }).click();

    // Wait for dialog to close (indicates success)
    await dialog.waitFor({ state: 'hidden' });
  }

  async getOpeningsTab() {
    await this.page.getByRole('tab', { name: 'Tee Time Openings' }).click();
  }
}
```

- [ ] **Step 2: Create WalkUpOfferPage fixture**

Create `src/web/e2e/fixtures/walk-up-offer-page.ts`:

```typescript
import { type Page, expect } from '@playwright/test';

export class WalkUpOfferPage {
  constructor(private readonly page: Page) {}

  async goto(offerPath: string) {
    await this.page.goto(offerPath);
  }

  async expectOfferDetails(courseName: string) {
    await expect(this.page.getByText(courseName)).toBeVisible();
    await expect(this.page.getByRole('button', { name: 'Claim This Tee Time' })).toBeVisible();
  }

  async claimTeeTime() {
    // Click the claim button
    await this.page.getByRole('button', { name: 'Claim This Tee Time' }).click();

    // Confirm in the alert dialog
    const dialog = this.page.getByRole('alertdialog');
    await dialog.getByRole('button', { name: 'Confirm' }).click();
  }

  async expectConfirmation() {
    await expect(this.page.getByText('Request Received')).toBeVisible();
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/web/e2e/fixtures/operator-waitlist-page.ts src/web/e2e/fixtures/walk-up-offer-page.ts
git commit -m "chore: add e2e page objects for tee time opening and offer acceptance"
```

---

### Task 5: E2E Tests — Add offer acceptance flow

**Files:**
- Modify: `src/web/e2e/tests/walkup/walkup-flow.spec.ts`

- [ ] **Step 1: Add the three new tests**

Update `src/web/e2e/tests/walkup/walkup-flow.spec.ts`:

```typescript
import { test, expect } from '../../fixtures/auth';
import { OperatorRegisterPage } from '../../fixtures/operator-register-page';
import { OperatorWaitlistPage } from '../../fixtures/operator-waitlist-page';
import { WalkupPage } from '../../fixtures/walkup-page';
import { WalkUpOfferPage } from '../../fixtures/walk-up-offer-page';
import { waitForSms, extractOfferUrl } from '../../fixtures/sms-helper';
import { TEST_TENANT_NAME, TEST_GOLFER, uniqueCourseName } from '../../fixtures/test-data';

const courseName = uniqueCourseName();
let walkupCode: string;
let offerPath: string;

test.describe.serial('Walkup Waitlist Flow', () => {
  test('operator registers a new course', async ({ page, asOperator }) => {
    await asOperator();
    const register = new OperatorRegisterPage(page);

    await register.goto();
    await register.selectTenant(TEST_TENANT_NAME);
    await register.registerCourse(courseName);
  });

  test('operator opens a walkup waitlist', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectTenant(TEST_TENANT_NAME);
    await waitlist.selectCourse(courseName);
    await waitlist.openWaitlist();

    walkupCode = await waitlist.getShortCode();
    expect(walkupCode).toBeTruthy();
    expect(walkupCode).toHaveLength(4);
  });

  test('golfer joins the waitlist via walkup code', async ({ page }) => {
    const walkup = new WalkupPage(page);

    await walkup.goto();
    await walkup.enterCode(walkupCode);

    // After valid code, the join form should appear
    await expect(page.getByLabel('First Name')).toBeVisible();

    await walkup.fillJoinForm(TEST_GOLFER);

    // Should see confirmation with position
    await expect(walkup.getConfirmationHeading()).toBeVisible();
    await expect(walkup.getPositionText()).toBeVisible();
  });

  test('operator adds a tee time opening', async ({ page, asOperator }) => {
    await asOperator();
    const waitlist = new OperatorWaitlistPage(page);

    await waitlist.goto();
    await waitlist.selectTenant(TEST_TENANT_NAME);
    await waitlist.selectCourse(courseName);

    // Add an opening 2 hours from now to avoid past-time validation
    const futureTime = new Date(Date.now() + 2 * 60 * 60 * 1000);
    const timeStr = `${String(futureTime.getHours()).padStart(2, '0')}:${String(futureTime.getMinutes()).padStart(2, '0')}`;
    await waitlist.addTeeTimeOpening(timeStr, 1);

    // Verify it appears in the Tee Time Openings tab
    await waitlist.getOpeningsTab();
    await expect(page.getByRole('cell', { name: /Open/i })).toBeVisible();
  });

  test('golfer receives offer via SMS', async () => {
    // Poll the dev SMS API for the offer message
    const smsBody = await waitForSms(
      TEST_GOLFER.phone,
      (body) => body.includes('/book/walkup/'),
      { timeoutMs: 20_000 },
    );

    offerPath = extractOfferUrl(smsBody);
    expect(offerPath).toMatch(/^\/book\/walkup\/[\w-]+$/);
  });

  test('golfer accepts the tee time offer', async ({ page }) => {
    const offerPage = new WalkUpOfferPage(page);

    await offerPage.goto(offerPath);
    await offerPage.expectOfferDetails(courseName);
    await offerPage.claimTeeTime();
    await offerPage.expectConfirmation();
  });
});
```

- [ ] **Step 2: Run the e2e tests locally (optional, requires test environment)**

Run:
```bash
E2E_BASE_URL=https://test.shadowbrook.golf E2E_API_URL=https://test-api.shadowbrook.golf pnpm --dir src/web exec playwright test --project=chromium
```

Expected: All 6 tests pass in serial order.

- [ ] **Step 3: Run frontend lint**

Run: `pnpm --dir src/web lint`

Expected: No lint errors.

- [ ] **Step 4: Commit**

```bash
git add src/web/e2e/tests/walkup/walkup-flow.spec.ts
git commit -m "test: add e2e tests for tee time opening creation and offer acceptance flow"
```
