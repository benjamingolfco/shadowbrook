# Issue #167 — Course Portfolio: Add "Change Organization" button

## Technical Plan

### Approach

The CoursePortfolio component renders outside of OperatorLayout when no course is selected (line 26 of `index.tsx`), so users lose access to the sidebar's "Change Organization" button. The fix adds a "Change Organization" button directly into the CoursePortfolio component, styled as a temporary dev/debug feature consistent with the DevRoleSwitcher pattern. The button calls `clearTenant()` from the already-imported `useTenantContext()` hook.

### Files

- **Modify:** `src/web/src/features/operator/pages/CoursePortfolio.tsx` — Add "Change Organization" button to all render states (loading, error, empty, populated)
- **Modify:** `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx` — Add tests for button visibility and clearTenant invocation

### Detailed Changes

#### `src/web/src/features/operator/pages/CoursePortfolio.tsx`

1. **Extract `clearTenant` from the existing `useTenantContext()` call** on line 19. Change from:
   ```
   const { tenant } = useTenantContext();
   ```
   to:
   ```
   const { tenant, clearTenant } = useTenantContext();
   ```

2. **Add a "Change Organization" button** to each of the four render branches (loading, error, empty, populated). The button should appear in every state because the user may want to switch organizations regardless of the loading/error state.

3. **Placement and styling**: Position the button at the top-right of the page container as a fixed-position dev tool, matching the DevRoleSwitcher's styling convention. Specifically:
   - Use a `<button>` element (via the `Button` component) with `variant="ghost"` and `size="sm"`
   - Add dev-tool visual styling: `fixed top-4 right-4 z-50 rounded-md bg-gray-800 px-3 py-2 text-xs font-medium text-white shadow-lg transition hover:bg-gray-700`
   - This mirrors the DevRoleSwitcher's dark-background dev-tool appearance (see `src/web/src/features/auth/components/DevRoleSwitcher.tsx`)

4. **Recommended implementation**: Rather than duplicating the button in all four render branches, add it once at the top of the component return. Restructure the component to always render the dev button, then conditionally render the content below. The cleanest approach is to extract the button into a small inline block placed outside the conditional returns. Since the component currently uses early returns for loading/error/empty states, the simplest approach without restructuring is to add the button to each branch. Alternatively, wrap all returns with a fragment and prepend the button.

   **Preferred approach** (minimal diff, no structural refactor): Add the button inside the existing `<div className="flex h-screen ...">` wrapper in each of the four render paths, positioned as `fixed` so it appears in the same spot regardless of branch. The button element is identical in all four branches:

   ```tsx
   <button
     onClick={clearTenant}
     className="fixed top-4 right-4 z-50 rounded-md bg-gray-800 px-3 py-2 text-xs font-medium text-white shadow-lg transition hover:bg-gray-700"
   >
     Change Organization
   </button>
   ```

   **Alternative preferred approach** (DRY, slight restructure): Wrap the component in a fragment to render the button once:

   ```tsx
   export default function CoursePortfolio() {
     // ... hooks ...

     const changeOrgButton = (
       <button
         onClick={clearTenant}
         className="fixed top-4 right-4 z-50 rounded-md bg-gray-800 px-3 py-2 text-xs font-medium text-white shadow-lg transition hover:bg-gray-700"
       >
         Change Organization
       </button>
     );

     if (coursesQuery.isLoading) {
       return (
         <>
           {changeOrgButton}
           <div className="flex h-screen items-center justify-center">
             ...
           </div>
         </>
       );
     }
     // ... same for other branches ...
   }
   ```

   Either approach is acceptable. The DRY approach (fragment wrapper) is preferred to avoid four identical button copies.

   **Note**: Use a raw `<button>` element with Tailwind classes (not the shadcn `Button` component) to match the DevRoleSwitcher convention exactly. The DevRoleSwitcher uses a raw `<select>` with the same dark styling classes.

### Styling Rationale

The DevRoleSwitcher pattern uses:
- `fixed` positioning (bottom-left corner: `bottom-4 left-4`)
- `z-50` to float above content
- Dark background: `bg-gray-800 hover:bg-gray-700`
- Small text: `text-xs font-medium text-white`
- Shadow: `shadow-lg`
- Rounded: `rounded-md`

The "Change Organization" button should use the same dark dev-tool styling but positioned at `top-4 right-4` to avoid colliding with the DevRoleSwitcher at `bottom-4 left-4`. This makes it immediately recognizable as a dev/debug control, not a permanent UI element.

### Testing Strategy

#### `src/web/src/features/operator/__tests__/CoursePortfolio.test.tsx`

Add the following test cases:

1. **"Change Organization" button is visible in loading state**: Render with `isLoading: true`, assert `screen.getByRole('button', { name: 'Change Organization' })` exists.

2. **"Change Organization" button is visible in error state**: Render with `isError: true`, assert button exists.

3. **"Change Organization" button is visible in empty state**: Render with `data: []`, assert button exists.

4. **"Change Organization" button is visible in populated state**: Render with courses data, assert button exists.

5. **Clicking "Change Organization" calls clearTenant**: Set up a `mockClearTenant` spy in the `beforeEach` block (it is already created as `vi.fn()` in the mock return value but not captured). Capture it:
   ```typescript
   const mockClearTenant = vi.fn();
   // in beforeEach:
   mockUseTenantContext.mockReturnValue({
     tenant: { id: 'tenant-1', organizationName: 'Pine Valley Golf Club' },
     selectTenant: vi.fn(),
     clearTenant: mockClearTenant,
   });
   ```
   Then click the button and assert `mockClearTenant` was called.

**Note**: The existing `beforeEach` already sets up `clearTenant: vi.fn()` but does not capture the mock into a variable. The implementer should extract `mockClearTenant` to a shared variable (similar to how `mockSelectCourse` is handled) so the new tests can assert on it.

### Integration Points

- **TenantContext**: The `clearTenant()` function (line 46-50 of `TenantContext.tsx`) sets tenant to `null`, removes from localStorage, and clears the active tenant ID. When tenant becomes `null`, the `TenantGate` component in `index.tsx` (line 44) will render `<OrganizationSelect />` instead of `<CourseProvider>/<CourseGate>`. No additional navigation logic is needed.

- **No router navigation required**: Clearing the tenant state causes React to re-render `TenantGate`, which unmounts `CourseGate`/`CoursePortfolio` and mounts `OrganizationSelect`. This is pure state-driven rendering, not URL-driven.

- **DevRoleSwitcher coexistence**: The DevRoleSwitcher is rendered in `App.tsx` at `fixed bottom-4 left-4`. The new button at `fixed top-4 right-4` will not conflict.

### Risks

- **None significant** — this is a 1-point XS change modifying a single component with well-understood behavior.
- The only consideration is ensuring the button appears in all four render branches of `CoursePortfolio`. If a new branch is added later, it should include the button too (the DRY fragment approach mitigates this).
