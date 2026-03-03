# Issue #196 -- Cannot see add waitlist request button

## Technical Plan

### Root Cause Analysis

The "Add to Waitlist" button is not missing due to a CSS or responsive layout issue. It is missing because of **conditional rendering gated on `waitlistEnabled`**.

In `Waitlist.tsx` (from PR #193 / branch `issue/180-walkup-waitlist`), the entire add-to-waitlist form -- including the submit button -- is only rendered when `waitlistEnabled` is `true`:

```tsx
{!isLoading && waitlistEnabled && (
  <>
    {/* date picker, summary card, ADD FORM WITH BUTTON, entries table */}
  </>
)}
```

The `Course.WaitlistEnabled` field defaults to `null`/`false` for all courses. The disabled state shows a dashed-border message: "Waitlist is not enabled for this course. Enable the waitlist in course settings to use this feature." However, the **Settings page (`TeeTimeSettings.tsx`) has no toggle to enable/disable the waitlist**. The only way to enable it is the `PUT /courses/{courseId}/waitlist-settings` API endpoint, which has no corresponding frontend control.

This means every operator who visits the waitlist page sees the disabled state with no way to enable the feature -- hence "there doesn't appear to be a button or anything to add a tee time to the waitlist."

### Approach

Add a waitlist enable/disable toggle directly on the Waitlist page itself, inside the disabled state callout. This is preferable to putting it on the Settings page because:

1. The operator is already looking at the waitlist page when they discover the feature is off.
2. It follows the progressive disclosure principle -- the toggle is contextual to where the feature lives.
3. It avoids sending the operator on a scavenger hunt to a different page.

The toggle should be a simple button (not a switch/checkbox) since this is a one-time activation action. Once enabled, the full waitlist UI (including the "Add to Waitlist" form and button) renders immediately.

Additionally, add an inline disable option within the enabled state so operators can turn the feature off if needed.

**Important:** This fix must be applied to the `issue/180-walkup-waitlist` branch (PR #193), not to `main`, since none of the waitlist code exists on `main` yet. The implementer should either:
- Apply these changes directly to the `issue/180-walkup-waitlist` branch, OR
- Merge PR #193 first, then apply fixes on a new branch off `main`.

### Files

All file paths below are relative to the `issue/180-walkup-waitlist` branch where the waitlist code lives.

- **Modify:** `src/web/src/features/operator/pages/Waitlist.tsx` -- Add enable button to disabled callout; add disable option in enabled state
- **Modify:** `src/web/src/features/operator/hooks/useWaitlist.ts` -- Add `useUpdateWaitlistSettings` mutation hook
- **Modify:** `src/web/src/features/operator/__tests__/Waitlist.test.tsx` -- Add tests for enable/disable toggle behavior

### Detailed Changes

#### 1. `src/web/src/features/operator/hooks/useWaitlist.ts`

Add a new mutation hook for the `PUT /courses/{courseId}/waitlist-settings` endpoint:

```typescript
export function useUpdateWaitlistSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, data }: { courseId: string; data: WaitlistSettings }) =>
      api.put<WaitlistSettings>(`/courses/${courseId}/waitlist-settings`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.waitlist.settings(courseId),
      });
    },
  });
}
```

This follows the same pattern as `useCreateWaitlistRequest` and `useUpdateTeeTimeSettings`.

#### 2. `src/web/src/features/operator/pages/Waitlist.tsx`

**2a. Import the new hook:**

Add `useUpdateWaitlistSettings` to the imports from `../hooks/useWaitlist`.

**2b. Initialize the mutation in the component body:**

```typescript
const updateSettingsMutation = useUpdateWaitlistSettings();
```

Place this alongside the other hooks, after `const createMutation = useCreateWaitlistRequest();`.

**2c. Replace the disabled state callout** (the `{!settingsQuery.isLoading && !settingsQuery.isError && !waitlistEnabled && (...)}` block).

Current code:
```tsx
{!settingsQuery.isLoading && !settingsQuery.isError && !waitlistEnabled && (
  <div className="mt-6 rounded-lg border-2 border-dashed border-muted-foreground/25 p-6 text-center">
    <p className="text-muted-foreground">Waitlist is not enabled for this course.</p>
    <p className="mt-1 text-sm text-muted-foreground">
      Enable the waitlist in course settings to use this feature.
    </p>
  </div>
)}
```

Replace with:
```tsx
{!settingsQuery.isLoading && !settingsQuery.isError && !waitlistEnabled && (
  <div className="mt-6 rounded-lg border-2 border-dashed border-muted-foreground/25 p-6 text-center">
    <p className="text-muted-foreground">Waitlist is not enabled for this course.</p>
    <p className="mt-1 text-sm text-muted-foreground">
      Enable the walk-up waitlist to start adding tee times.
    </p>
    <Button
      className="mt-4"
      onClick={() => {
        if (!course) return;
        updateSettingsMutation.mutate({
          courseId: course.id,
          data: { waitlistEnabled: true },
        });
      }}
      disabled={updateSettingsMutation.isPending}
    >
      {updateSettingsMutation.isPending ? 'Enabling...' : 'Enable Waitlist'}
    </Button>
    {updateSettingsMutation.isError && (
      <p className="mt-2 text-sm text-destructive">
        {updateSettingsMutation.error instanceof Error
          ? updateSettingsMutation.error.message
          : 'Failed to enable waitlist.'}
      </p>
    )}
  </div>
)}
```

Key details:
- The button text is "Enable Waitlist" -- clear, action-oriented.
- The description text changes from "Enable the waitlist in course settings" (which pointed to a nonexistent UI) to "Enable the walk-up waitlist to start adding tee times."
- Shows loading state while the mutation is in progress.
- Shows error state if the mutation fails.
- On success, `invalidateQueries` on waitlist settings will cause `settingsQuery` to refetch, `waitlistEnabled` flips to `true`, and the conditional rendering switches to show the full UI with the "Add to Waitlist" form.

**2d. Add a disable option in the enabled state** (optional, low priority):

At the top of the enabled state block, after the page header (`<h1>` and `<p>` tags), add a small text button to disable:

```tsx
<div className="flex items-center justify-between">
  <div>
    <h1 className="text-2xl font-bold">Waitlist</h1>
    <p className="text-muted-foreground">Manage walk-up golfers for open tee times</p>
  </div>
</div>
```

This is a minor enhancement and can be deferred. The critical fix is the enable button in step 2c.

#### 3. `src/web/src/features/operator/__tests__/Waitlist.test.tsx`

Add the following tests:

**3a. "shows enable button when waitlist is disabled":**
```typescript
it('shows enable button when waitlist is disabled', () => {
  mockUseWaitlistSettings.mockReturnValue(disabledSettingsReturn);
  render(<Waitlist />);
  expect(screen.getByRole('button', { name: 'Enable Waitlist' })).toBeInTheDocument();
});
```

**3b. "enable button calls updateSettings mutation":**
```typescript
it('enable button calls updateSettings mutation', async () => {
  mockUseWaitlistSettings.mockReturnValue(disabledSettingsReturn);
  const mockMutate = vi.fn();
  mockUseUpdateWaitlistSettings.mockReturnValue({
    mutate: mockMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useUpdateWaitlistSettings>);

  render(<Waitlist />);
  await userEvent.click(screen.getByRole('button', { name: 'Enable Waitlist' }));
  expect(mockMutate).toHaveBeenCalledWith({
    courseId: 'course-1',
    data: { waitlistEnabled: true },
  });
});
```

**3c. "enable button shows loading state while enabling":**
```typescript
it('enable button shows loading state while enabling', () => {
  mockUseWaitlistSettings.mockReturnValue(disabledSettingsReturn);
  mockUseUpdateWaitlistSettings.mockReturnValue({
    mutate: vi.fn(),
    isPending: true,
    isSuccess: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useUpdateWaitlistSettings>);

  render(<Waitlist />);
  const button = screen.getByRole('button', { name: 'Enabling...' });
  expect(button).toBeDisabled();
});
```

**3d. Update test setup:** Add `useUpdateWaitlistSettings` to the mock imports and `beforeEach`. Add a `defaultUpdateSettingsMutation` return value similar to `defaultMutationReturn`.

**3e. Update the existing "feature disabled callout" test** to also verify the enable button is present (or keep it as-is and let test 3a cover the button separately).

### Patterns

- **Mutation hook pattern:** Follows the established pattern in `useCreateWaitlistRequest` and `useUpdateTeeTimeSettings` -- `useMutation` with `onSuccess` cache invalidation.
- **Optimistic UI via cache invalidation:** After the PUT succeeds, `invalidateQueries` on the settings key triggers a refetch. The component re-renders with `waitlistEnabled: true`, which flips the conditional to show the full UI. No manual state manipulation needed.
- **Error handling pattern:** Consistent with all other mutation error displays in the codebase (check `instanceof Error`, fall back to generic message).

### API Design

No new endpoints needed. The `PUT /courses/{courseId}/waitlist-settings` endpoint already exists in the `WaitlistEndpoints.cs` from PR #193. It accepts `{ waitlistEnabled: boolean }` and returns the same shape.

### Testing Strategy

**Automated tests (3-4 new tests):**
- Enable button renders when waitlist is disabled
- Clicking enable button calls the mutation with correct payload
- Enable button shows disabled/loading state during pending mutation
- Error message renders on mutation failure

**Manual testing steps:**
1. Navigate to the waitlist page with a course that has waitlist disabled (the default state for all courses)
2. Verify the "Enable Waitlist" button is visible in the disabled callout
3. Click the button and verify the waitlist UI (including "Add to Waitlist" form) appears
4. Verify the button shows "Enabling..." while the API call is in progress
5. Test on mobile viewport (< 768px): open sidebar via hamburger, navigate to Waitlist, verify the enable button is visible and tappable
6. Test on desktop viewport: verify the same flow works with the sidebar always visible

**Edge cases:**
- Network error during enable: verify error message appears below the button
- Rapidly clicking enable: verify the button is disabled during the pending state
- Course with no tee time settings configured: verify waitlist can be enabled independently (the API does not require tee time settings)

### Risks

1. **Branch targeting:** This fix must target the `issue/180-walkup-waitlist` branch, not `main`. If the implementer creates changes on `main`, the waitlist page does not exist there and the changes will have no effect.
2. **Test mock setup:** The `useUpdateWaitlistSettings` hook must be added to the existing mock structure in the test file. Missing the mock will cause all existing tests to fail if the hook is called during render.
3. **Low risk overall:** This is a small, additive change with no data model modifications, no new endpoints, and no migration impact. The existing API endpoint and frontend infrastructure already support the fix.
