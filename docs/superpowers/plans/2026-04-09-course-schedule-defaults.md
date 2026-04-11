# Course Schedule Defaults Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `DefaultCapacity` to the tee-time-settings endpoint and form, and relax the interval validator from a fixed 8/10/12 enum to any positive integer.

**Architecture:** Extend existing DTOs, validator, and handlers — no new endpoints or pages. The domain already supports all four fields; this plan wires them through the API and frontend layers.

**Tech Stack:** .NET 10 (FluentValidation, Wolverine HTTP), React 19 (Zod, React Hook Form, shadcn/ui)

**Spec:** `docs/superpowers/specs/2026-04-09-course-schedule-defaults-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs` | DTOs + handlers |
| Modify | `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/TeeTimeSettingsRequestValidatorTests.cs` | Validator unit tests |
| Modify | `src/web/src/types/course.ts` | TypeScript interface |
| Modify | `src/web/src/features/operator/pages/TeeTimeSettings.tsx` | Form UI + Zod schema |
| Modify | `src/web/src/features/operator/hooks/useTeeTimeSettings.ts` | No structural changes — verify type flows |

---

### Task 1: Update Validator Tests (Red)

**Files:**
- Modify: `tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/TeeTimeSettingsRequestValidatorTests.cs`

- [ ] **Step 1: Update existing tests to use new DTO shape and new interval rules**

The DTO will gain a fourth parameter `DefaultCapacity`. Update all existing test calls and change interval expectations: 5 and 15 should now pass (any positive int), only 0 and -1 should fail.

```csharp
using FluentValidation.TestHelper;
using Teeforce.Api.Features.Courses;
using static Teeforce.Api.Features.Courses.CourseEndpoints;

namespace Teeforce.Api.Tests.Features.TeeSheet.Validators;

public class TeeTimeSettingsRequestValidatorTests
{
    private readonly TeeTimeSettingsRequestValidator validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), 4))
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(15)]
    public void Positive_Interval_Passes(int interval) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(interval, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), 4))
            .ShouldNotHaveValidationErrorFor(x => x.TeeTimeIntervalMinutes);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_Interval_Fails(int interval) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(interval, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), 4))
            .ShouldHaveValidationErrorFor(x => x.TeeTimeIntervalMinutes);

    [Fact]
    public void FirstTeeTime_After_LastTeeTime_Fails() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("18:00"), TimeOnly.Parse("07:00"), 4))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);

    [Fact]
    public void FirstTeeTime_Equals_LastTeeTime_Fails() =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("07:00"), 4))
            .ShouldHaveValidationErrorFor(x => x.FirstTeeTime);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void Positive_DefaultCapacity_Passes(int capacity) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), capacity))
            .ShouldNotHaveValidationErrorFor(x => x.DefaultCapacity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_Positive_DefaultCapacity_Fails(int capacity) =>
        this.validator.TestValidate(new TeeTimeSettingsRequest(10, TimeOnly.Parse("07:00"), TimeOnly.Parse("18:00"), capacity))
            .ShouldHaveValidationErrorFor(x => x.DefaultCapacity);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~TeeTimeSettingsRequestValidatorTests" --no-restore -v minimal`

Expected: Compilation error — `TeeTimeSettingsRequest` doesn't accept 4 parameters yet.

- [ ] **Step 3: Commit red tests**

```bash
git add tests/Teeforce.Api.Tests/Features/TeeSheet/Validators/TeeTimeSettingsRequestValidatorTests.cs
git commit -m "test: red — update validator tests for DefaultCapacity and relaxed interval"
```

---

### Task 2: Update Backend DTOs, Validator, and Handlers (Green)

**Files:**
- Modify: `src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs:165-207,276-349`

- [ ] **Step 1: Add DefaultCapacity to DTOs**

In `CourseEndpoints.cs`, update the two records (around lines 276-284):

```csharp
public record TeeTimeSettingsRequest(
    int TeeTimeIntervalMinutes,
    TimeOnly FirstTeeTime,
    TimeOnly LastTeeTime,
    int DefaultCapacity);

public record TeeTimeSettingsResponse(
    int TeeTimeIntervalMinutes,
    TimeOnly FirstTeeTime,
    TimeOnly LastTeeTime,
    int DefaultCapacity);
```

- [ ] **Step 2: Relax interval validator and add DefaultCapacity rule**

Replace the `TeeTimeSettingsRequestValidator` class (lines 336-349):

```csharp
public class TeeTimeSettingsRequestValidator : AbstractValidator<TeeTimeSettingsRequest>
{
    public TeeTimeSettingsRequestValidator()
    {
        RuleFor(x => x.TeeTimeIntervalMinutes)
            .GreaterThan(0)
            .WithMessage("Interval must be greater than 0.");
        RuleFor(x => x.FirstTeeTime)
            .LessThan(x => x.LastTeeTime)
            .WithMessage("First tee time must be before last tee time.");
        RuleFor(x => x.DefaultCapacity)
            .GreaterThan(0)
            .WithMessage("Default capacity must be greater than 0.");
    }
}
```

- [ ] **Step 3: Update PUT handler to call UpdateDefaultCapacity**

Replace the PUT handler body (lines 165-185). After `UpdateTeeTimeSettings`, call `UpdateDefaultCapacity`, and include `DefaultCapacity` in the response:

```csharp
[WolverinePut("/courses/{courseId}/tee-time-settings")]
[Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
public static async Task<IResult> UpdateTeeTimeSettings(
    Guid courseId,
    TeeTimeSettingsRequest request,
    ApplicationDbContext db)
{
    var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);

    if (course is null)
    {
        return Results.NotFound(new { error = "Course not found." });
    }

    course.UpdateTeeTimeSettings(request.TeeTimeIntervalMinutes, request.FirstTeeTime, request.LastTeeTime);
    course.UpdateDefaultCapacity(request.DefaultCapacity);

    return Results.Ok(new TeeTimeSettingsResponse(
        course.TeeTimeIntervalMinutes!.Value,
        course.FirstTeeTime!.Value,
        course.LastTeeTime!.Value,
        course.DefaultCapacity));
}
```

- [ ] **Step 4: Update GET handler to include DefaultCapacity**

Replace the GET handler body (lines 187-207). Include `DefaultCapacity` in the response:

```csharp
[WolverineGet("/courses/{courseId}/tee-time-settings")]
[Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
public static async Task<IResult> GetTeeTimeSettings(Guid courseId, ApplicationDbContext db)
{
    var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);

    if (course is null)
    {
        return Results.NotFound(new { error = "Course not found." });
    }

    if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
    {
        return Results.Ok(new { });
    }

    return Results.Ok(new TeeTimeSettingsResponse(
        course.TeeTimeIntervalMinutes.Value,
        course.FirstTeeTime.Value,
        course.LastTeeTime.Value,
        course.DefaultCapacity));
}
```

- [ ] **Step 5: Build to verify compilation**

Run: `dotnet build teeforce.slnx`

Expected: BUILD SUCCEEDED

- [ ] **Step 6: Run validator tests to verify they pass**

Run: `dotnet test tests/Teeforce.Api.Tests --filter "FullyQualifiedName~TeeTimeSettingsRequestValidatorTests" --no-restore -v minimal`

Expected: All 11 tests pass.

- [ ] **Step 7: Run full backend test suite**

Run: `dotnet test teeforce.slnx --no-restore -v minimal`

Expected: All tests pass. No other tests should reference the old 3-param DTO.

- [ ] **Step 8: Format**

Run: `dotnet format teeforce.slnx`

- [ ] **Step 9: Commit green backend**

```bash
git add src/backend/Teeforce.Api/Features/Courses/CourseEndpoints.cs
git commit -m "feat: add DefaultCapacity to tee-time-settings DTOs, relax interval validator"
```

---

### Task 3: Update Frontend Type and Hook

**Files:**
- Modify: `src/web/src/types/course.ts:17-21`
- Verify: `src/web/src/features/operator/hooks/useTeeTimeSettings.ts`

- [ ] **Step 1: Add defaultCapacity to TeeTimeSettings interface**

In `src/web/src/types/course.ts`, update the interface:

```typescript
export interface TeeTimeSettings {
  teeTimeIntervalMinutes: number;
  firstTeeTime: string;
  lastTeeTime: string;
  defaultCapacity: number;
}
```

- [ ] **Step 2: Verify the hook needs no changes**

Read `src/web/src/features/operator/hooks/useTeeTimeSettings.ts` — it uses `TeeTimeSettings` generically in `api.get<TeeTimeSettings>` and `api.put<TeeTimeSettings>`. The new field flows through automatically. No changes needed.

- [ ] **Step 3: Run frontend lint to verify**

Run: `pnpm --dir src/web lint`

Expected: No errors (the type is used but TeeTimeSettings.tsx will need updates — lint may show warnings about the form not using the new field yet, but no errors).

- [ ] **Step 4: Commit**

```bash
git add src/web/src/types/course.ts
git commit -m "feat: add defaultCapacity to TeeTimeSettings type"
```

---

### Task 4: Update Frontend Form

**Files:**
- Modify: `src/web/src/features/operator/pages/TeeTimeSettings.tsx`

- [ ] **Step 1: Install shadcn Alert component**

The spec calls for a yellow warning when interval < 8. Add the Alert component:

Run: `pnpm --dir src/web dlx shadcn@latest add alert`

- [ ] **Step 2: Replace the full TeeTimeSettings.tsx file**

Replace the entire file content. Key changes:
- Zod schema: interval changes from `refine([8,10,12])` to `z.number().int().min(1)`, add `defaultCapacity: z.number().int().min(1)`
- Interval field: `<Select>` → `<Input type="number">` with min=1
- Warning: yellow `<Alert>` when interval < 8
- New field: "Default Group Size" number input
- Form reset: include `defaultCapacity` from query data

```tsx
import { useEffect } from 'react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  useTeeTimeSettings,
  useUpdateTeeTimeSettings,
} from '../hooks/useTeeTimeSettings';
import { useCourseContext } from '../context/CourseContext';

const teeTimeSettingsSchema = z.object({
  teeTimeIntervalMinutes: z.number().int().min(1, 'Interval must be at least 1 minute'),
  firstTeeTime: z.string().min(1, 'First tee time is required'),
  lastTeeTime: z.string().min(1, 'Last tee time is required'),
  defaultCapacity: z.number().int().min(1, 'Must be at least 1'),
});

type TeeTimeSettingsFormData = z.infer<typeof teeTimeSettingsSchema>;

export default function TeeTimeSettings() {
  const { course, registerDirtyForm, unregisterDirtyForm } = useCourseContext();

  const form = useForm<TeeTimeSettingsFormData>({
    resolver: zodResolver(teeTimeSettingsSchema),
    defaultValues: {
      teeTimeIntervalMinutes: 10,
      firstTeeTime: '07:00',
      lastTeeTime: '18:00',
      defaultCapacity: 4,
    },
  });

  const settingsQuery = useTeeTimeSettings(course?.id);
  const updateMutation = useUpdateTeeTimeSettings();

  const formIsDirty = form.formState.isDirty;
  const intervalValue = form.watch('teeTimeIntervalMinutes');

  useEffect(() => {
    if (formIsDirty) {
      registerDirtyForm('tee-time-settings');
    } else {
      unregisterDirtyForm('tee-time-settings');
    }
    return () => {
      unregisterDirtyForm('tee-time-settings');
    };
  }, [formIsDirty, registerDirtyForm, unregisterDirtyForm]);

  useEffect(() => {
    if (settingsQuery.data?.firstTeeTime && settingsQuery.data.lastTeeTime) {
      form.reset({
        teeTimeIntervalMinutes: settingsQuery.data.teeTimeIntervalMinutes,
        firstTeeTime: settingsQuery.data.firstTeeTime.slice(0, 5),
        lastTeeTime: settingsQuery.data.lastTeeTime.slice(0, 5),
        defaultCapacity: settingsQuery.data.defaultCapacity,
      });
    }
  }, [settingsQuery.data, form]);

  if (!course) return null;

  const courseId = course.id;

  function onSubmit(data: TeeTimeSettingsFormData) {
    updateMutation.mutate({ courseId, data });
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Tee Time Settings</h1>}
      />

      <div className="max-w-2xl">
        <Card className="border-border-strong">
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Tee Time Configuration
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                {settingsQuery.isLoading && (
                  <p className="text-ink-muted text-sm">Loading settings…</p>
                )}
                {settingsQuery.isError && (
                  <p className="text-destructive text-sm">
                    Error loading settings: {settingsQuery.error.message}
                  </p>
                )}

                <FormField
                  control={form.control}
                  name="teeTimeIntervalMinutes"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Tee Time Interval (minutes)</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={1}
                          {...field}
                          onChange={(e) => field.onChange(Number(e.target.value))}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                {intervalValue > 0 && intervalValue < 8 && (
                  <Alert variant="warning">
                    <AlertDescription>
                      Most courses use intervals of 8 minutes or more. Short intervals may cause pace-of-play issues.
                    </AlertDescription>
                  </Alert>
                )}

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    control={form.control}
                    name="firstTeeTime"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>First Tee Time</FormLabel>
                        <FormControl>
                          <Input type="time" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="lastTeeTime"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Last Tee Time</FormLabel>
                        <FormControl>
                          <Input type="time" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                <FormField
                  control={form.control}
                  name="defaultCapacity"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Default Group Size</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={1}
                          {...field}
                          onChange={(e) => field.onChange(Number(e.target.value))}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                {updateMutation.isError && (
                  <p className="text-destructive text-sm">
                    Error: {updateMutation.error.message}
                  </p>
                )}

                {updateMutation.isSuccess && (
                  <p className="text-green text-sm">
                    Tee time settings saved successfully!
                  </p>
                )}

                <Button type="submit" disabled={updateMutation.isPending}>
                  {updateMutation.isPending ? 'Saving…' : 'Save Settings'}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
```

- [ ] **Step 3: Check if shadcn Alert has a "warning" variant**

The stock shadcn `Alert` ships with `default` and `destructive` variants. If `warning` is missing, add a wrapper. Create `src/web/src/components/ui/alert.tsx` variant check — if it only has `default | destructive`, add `warning` to the variants in `alert.tsx`:

```typescript
// In the alertVariants cva call, add to variants.variant:
warning: "border-amber-200 bg-amber-50 text-amber-900 [&>svg]:text-amber-600",
```

If the Alert component doesn't exist yet (it didn't show in glob), the `shadcn add alert` command from step 1 will create it — then add the `warning` variant.

- [ ] **Step 4: Run frontend lint**

Run: `pnpm --dir src/web lint`

Expected: No errors.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/components/ui/alert.tsx src/web/src/features/operator/pages/TeeTimeSettings.tsx
git commit -m "feat: add DefaultCapacity field and number interval input to tee time settings form"
```

---

### Task 5: Smoke Test with make dev

- [ ] **Step 1: Start the app**

Run: `make dev`

- [ ] **Step 2: Verify manually**

1. Navigate to operator settings → Tee Time Settings
2. Confirm interval is now a number input (not a dropdown)
3. Enter interval < 8 → yellow warning appears
4. Confirm "Default Group Size" field is visible with value 4
5. Save with valid values → success message
6. Reload page → values persist

- [ ] **Step 3: Stop the app and commit if any fixes were needed**

---

### Task 6: Final Commit and PR

- [ ] **Step 1: Run full test suite**

Run: `dotnet test teeforce.slnx --no-restore -v minimal && pnpm --dir src/web lint`

Expected: All green.

- [ ] **Step 2: Create PR**

```bash
gh pr create --title "feat: course schedule defaults (capacity + flexible interval)" --body "$(cat <<'EOF'
## Summary
- Add `DefaultCapacity` to tee-time-settings GET/PUT endpoint DTOs
- Relax interval validator from fixed 8/10/12 to any positive integer
- Add "Default Group Size" number input to operator settings form
- Show soft warning when interval < 8 minutes

Closes #397

## Test plan
- [ ] Validator unit tests cover positive/non-positive for both interval and capacity
- [ ] Save settings with custom interval (e.g. 7) — warning shown, saves OK
- [ ] Save settings with capacity 2 — persists and reloads correctly
- [ ] Existing 8/10/12 intervals still work

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
