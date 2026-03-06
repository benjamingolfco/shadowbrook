# Operator Screen Pattern & Walkup Waitlist UI Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a reusable `PageHeader` component and refactor the walkup waitlist page to use an action bar + dialog pattern.

**Architecture:** Extract a `PageHeader` layout component, then refactor `WalkUpWaitlist.tsx` by pulling the open/close/add-request actions into standalone dialog components. The page becomes a thin orchestrator: header with context-aware actions, data tables below. No hook/API changes.

**Tech Stack:** React 19, TypeScript, shadcn/ui (Dialog, AlertDialog, Button, Badge), React Hook Form + Zod, TanStack Query, Vitest + React Testing Library

---

### Task 1: Create PageHeader Component

**Files:**
- Create: `src/web/src/components/layout/PageHeader.tsx`
- Test: `src/web/src/components/layout/__tests__/PageHeader.test.tsx`

**Step 1: Write the test**

Create `src/web/src/components/layout/__tests__/PageHeader.test.tsx`:

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import { PageHeader } from '../PageHeader';

describe('PageHeader', () => {
  it('renders the title', () => {
    render(<PageHeader title="Walk-Up Waitlist" />);
    expect(screen.getByRole('heading', { name: 'Walk-Up Waitlist' })).toBeInTheDocument();
  });

  it('renders children in the subtitle area', () => {
    render(
      <PageHeader title="Walk-Up Waitlist">
        <span>Short Code: 4827</span>
      </PageHeader>,
    );
    expect(screen.getByText('Short Code: 4827')).toBeInTheDocument();
  });

  it('renders actions', () => {
    render(
      <PageHeader
        title="Walk-Up Waitlist"
        actions={<button>Open Waitlist</button>}
      />,
    );
    expect(screen.getByRole('button', { name: 'Open Waitlist' })).toBeInTheDocument();
  });

  it('renders title, children, and actions together', () => {
    render(
      <PageHeader
        title="Walk-Up Waitlist"
        actions={<button>Action</button>}
      >
        <span>subtitle</span>
      </PageHeader>,
    );
    expect(screen.getByRole('heading', { name: 'Walk-Up Waitlist' })).toBeInTheDocument();
    expect(screen.getByText('subtitle')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Action' })).toBeInTheDocument();
  });
});
```

**Step 2: Run test to verify it fails**

Run: `pnpm --dir src/web test src/web/src/components/layout/__tests__/PageHeader.test.tsx`
Expected: FAIL — module not found

**Step 3: Write the component**

Create `src/web/src/components/layout/PageHeader.tsx`:

```tsx
import type { ReactNode } from 'react';

interface PageHeaderProps {
  title: string;
  actions?: ReactNode;
  children?: ReactNode;
}

export function PageHeader({ title, actions, children }: PageHeaderProps) {
  return (
    <div className="mb-6">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold">{title}</h1>
          {children && <div className="mt-1">{children}</div>}
        </div>
        {actions && (
          <div className="flex items-center gap-2 shrink-0">{actions}</div>
        )}
      </div>
    </div>
  );
}
```

**Step 4: Run test to verify it passes**

Run: `pnpm --dir src/web test src/web/src/components/layout/__tests__/PageHeader.test.tsx`
Expected: PASS (4 tests)

**Step 5: Lint**

Run: `pnpm --dir src/web lint`
Expected: PASS

**Step 6: Commit**

```bash
git add src/web/src/components/layout/PageHeader.tsx src/web/src/components/layout/__tests__/PageHeader.test.tsx
git commit -m "feat(ui): add reusable PageHeader component"
```

---

### Task 2: Create OpenWaitlistDialog Component

**Files:**
- Create: `src/web/src/features/operator/components/OpenWaitlistDialog.tsx`

**Step 1: Create the component**

```tsx
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';

interface OpenWaitlistDialogProps {
  onConfirm: () => void;
  isPending: boolean;
}

export function OpenWaitlistDialog({ onConfirm, isPending }: OpenWaitlistDialogProps) {
  var [open, setOpen] = useState(false);

  function handleConfirm() {
    onConfirm();
    setOpen(false);
  }

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>
        <Button disabled={isPending}>
          {isPending ? 'Opening...' : 'Open Waitlist'}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Open Walk-Up Waitlist</AlertDialogTitle>
          <AlertDialogDescription>
            This will open the walk-up waitlist for today and generate a short code for golfers to join.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={handleConfirm}>
            Open Waitlist
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
```

**Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: PASS

**Step 3: Commit**

```bash
git add src/web/src/features/operator/components/OpenWaitlistDialog.tsx
git commit -m "feat(operator): add OpenWaitlistDialog component"
```

---

### Task 3: Create AddTeeTimeRequestDialog Component

**Files:**
- Create: `src/web/src/features/operator/components/AddTeeTimeRequestDialog.tsx`

**Step 1: Create the component**

Extract the form from `TeeTimeRequestsSection` into a dialog. Uses the existing `useCreateWaitlistRequest` hook and Zod schema.

```tsx
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { useCreateWaitlistRequest } from '../hooks/useWaitlist';

var addTeeTimeRequestSchema = z.object({
  teeTime: z.string().min(1, 'Tee time is required'),
  golfersNeeded: z.number().min(1, 'At least 1 golfer needed').max(4, 'Maximum 4 golfers'),
});

type AddTeeTimeRequestFormData = z.infer<typeof addTeeTimeRequestSchema>;

function getTodayDate(): string {
  var today = new Date();
  var isoString = today.toISOString().split('T')[0];
  return isoString ?? '';
}

interface AddTeeTimeRequestDialogProps {
  courseId: string;
}

export function AddTeeTimeRequestDialog({ courseId }: AddTeeTimeRequestDialogProps) {
  var [open, setOpen] = useState(false);
  var createMutation = useCreateWaitlistRequest();
  var todayDate = getTodayDate();

  var form = useForm<AddTeeTimeRequestFormData>({
    resolver: zodResolver(addTeeTimeRequestSchema),
    defaultValues: {
      teeTime: '',
      golfersNeeded: 1,
    },
  });

  function onSubmit(data: AddTeeTimeRequestFormData) {
    createMutation.mutate(
      {
        courseId,
        data: {
          date: todayDate,
          teeTime: data.teeTime,
          golfersNeeded: data.golfersNeeded,
        },
      },
      {
        onSuccess: () => {
          form.reset({ teeTime: '', golfersNeeded: 1 });
          setOpen(false);
        },
      },
    );
  }

  function handleOpenChange(nextOpen: boolean) {
    setOpen(nextOpen);
    if (!nextOpen) {
      form.reset({ teeTime: '', golfersNeeded: 1 });
      createMutation.reset();
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button variant="outline">Add Tee Time Request</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Tee Time Request</DialogTitle>
          <DialogDescription>
            Add a tee time request to the waitlist for today.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <FormField
              control={form.control}
              name="teeTime"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Tee Time</FormLabel>
                  <FormControl>
                    <Input type="time" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="golfersNeeded"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Golfers Needed</FormLabel>
                  <Select
                    value={String(field.value)}
                    onValueChange={(v) => field.onChange(Number(v))}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value="1">1</SelectItem>
                      <SelectItem value="2">2</SelectItem>
                      <SelectItem value="3">3</SelectItem>
                      <SelectItem value="4">4</SelectItem>
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            {createMutation.isError && (
              <p className="text-sm text-destructive" role="alert">
                {createMutation.error instanceof Error
                  ? createMutation.error.message
                  : 'Failed to add tee time to waitlist.'}
              </p>
            )}

            <DialogFooter>
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Adding...' : 'Add Request'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
```

**Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: PASS

**Step 3: Commit**

```bash
git add src/web/src/features/operator/components/AddTeeTimeRequestDialog.tsx
git commit -m "feat(operator): add AddTeeTimeRequestDialog component"
```

---

### Task 4: Create CloseWaitlistDialog Component

**Files:**
- Create: `src/web/src/features/operator/components/CloseWaitlistDialog.tsx`

**Step 1: Create the component**

Extract the existing inline AlertDialog into its own component.

```tsx
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';

interface CloseWaitlistDialogProps {
  onConfirm: () => void;
  isPending: boolean;
}

export function CloseWaitlistDialog({ onConfirm, isPending }: CloseWaitlistDialogProps) {
  return (
    <AlertDialog>
      <AlertDialogTrigger asChild>
        <Button variant="destructive" disabled={isPending}>
          {isPending ? 'Closing...' : 'Close Waitlist'}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Close Walk-Up Waitlist?</AlertDialogTitle>
          <AlertDialogDescription>
            No new golfers will be able to join. Existing entries will be preserved.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel autoFocus>Keep Open</AlertDialogCancel>
          <AlertDialogAction
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            onClick={onConfirm}
          >
            Close Waitlist
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
```

**Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: PASS

**Step 3: Commit**

```bash
git add src/web/src/features/operator/components/CloseWaitlistDialog.tsx
git commit -m "feat(operator): add CloseWaitlistDialog component"
```

---

### Task 5: Refactor WalkUpWaitlist Page

**Files:**
- Modify: `src/web/src/features/operator/pages/WalkUpWaitlist.tsx`

**Step 1: Rewrite the page**

Replace the entire file. The page becomes a thin orchestrator using PageHeader + dialog components. The `QueueTable` and `TeeTimeRequestsSection` (now read-only, no form) remain as local helper components.

Key changes:
- Import and use `PageHeader` for all states (loading, error, inactive, active, closed)
- Import and use `OpenWaitlistDialog`, `AddTeeTimeRequestDialog`, `CloseWaitlistDialog`
- Remove the inline form from `TeeTimeRequestsSection` (the `readOnly` prop and all form logic go away)
- Move short code display + copy button to the PageHeader children slot
- Move error messages for open/close mutations below the header
- Remove the `addWaitlistRequestSchema`, `AddWaitlistRequestFormData`, and all form-related imports that moved to the dialog

The `TeeTimeRequestsSection` becomes purely a data display:
- Shows the "Total Golfers Pending" card
- Shows the tee time requests table
- Shows loading/error/empty states
- No form, no `readOnly` prop

The `QueueTable` component stays unchanged.

The `getTodayDate`, `formatJoinedAt`, and `formatTime` helper functions stay in this file.

**Step 2: Run existing tests**

Run: `pnpm --dir src/web test src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`

Some tests will fail because the UI structure changed (e.g., "Add to Waitlist" heading/button are now in a dialog, "Open Waitlist" is now behind a dialog confirmation). Note which tests fail.

**Step 3: Lint**

Run: `pnpm --dir src/web lint`
Expected: PASS

**Step 4: Commit**

```bash
git add src/web/src/features/operator/pages/WalkUpWaitlist.tsx
git commit -m "refactor(operator): rewrite WalkUpWaitlist with PageHeader and dialog actions"
```

---

### Task 6: Update Tests for Refactored Page

**Files:**
- Modify: `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`

**Step 1: Update the tests**

The test file needs updates to match the new UI structure:

1. **"Open Waitlist" button now opens a dialog first** — tests that check for the open button still pass (the trigger button text is the same). Tests that check `mockOpenMutate` was called need to also click through the dialog confirmation.

2. **"Add to Waitlist" form is now in a dialog** — tests that look for "Add to Waitlist" heading/button need to first click "Add Tee Time Request" to open the dialog. The `useCreateWaitlistRequest` mock is now used inside the dialog component, so tests that mock it need the dialog to be open.

3. **"Close Waitlist" dialog** — behavior is the same (still an AlertDialog), but the trigger is now in the PageHeader actions area instead of at the bottom of the page.

4. **New tests needed**:
   - Inactive state shows "Open Waitlist" button in the header actions
   - Clicking "Open Waitlist" opens a confirmation dialog
   - Active state shows "Add Tee Time Request" and "Close Waitlist" buttons in header
   - Closed state shows no action buttons

5. **Tests to remove or update**:
   - "shows tee time requests section with form when waitlist is open" — form is no longer inline, update to check for "Add Tee Time Request" button instead
   - "shows tee time requests section without form when waitlist is closed" — update to check that no action buttons appear in header
   - "disables add button during mutation" — this now happens inside the dialog
   - "shows success message after adding tee time" — dialog closes on success instead
   - "shows error message after failed tee time submission" — error shows inside dialog

**Step 2: Run tests to verify all pass**

Run: `pnpm --dir src/web test src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`
Expected: ALL PASS

**Step 3: Run full test suite**

Run: `pnpm --dir src/web test`
Expected: ALL PASS

**Step 4: Lint**

Run: `pnpm --dir src/web lint`
Expected: PASS

**Step 5: Commit**

```bash
git add src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx
git commit -m "test(operator): update WalkUpWaitlist tests for dialog-based actions"
```

---

## Design Reference

See `docs/plans/2026-03-06-operator-screen-pattern-design.md` for the approved design decisions.
