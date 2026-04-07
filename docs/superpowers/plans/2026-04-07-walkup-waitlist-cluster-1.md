# Walk-up Waitlist Redesign — Cluster 1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle and restructure the operator-side walk-up waitlist page (`WalkUpWaitlist.tsx`) into the Fieldstone design language inside the existing minimal `AppShell`, with topbar slot contributions and a TeeSheet-style openings grid. No new functionality, no new tests.

**Architecture:** The page mounts inside the existing `WaitlistShellLayout` (untouched) and contributes topbar middle/right content via `<PageTopbar>` portals from a new `WalkUpWaitlistTopbar` page-internal component. The openings list becomes a new `OpeningsGrid` component that mirrors `TeeSheetGrid`'s row idiom. The queue stays inside the existing `QueueDrawer` (restyled, trigger relocated to topbar middle slot).

**Tech Stack:** React 19, TypeScript 5.9, Tailwind CSS v4, shadcn/ui (vendored, read-only), TanStack Query, Vitest + React Testing Library, lucide-react icons.

**Spec:** [`docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md`](../specs/2026-04-07-walkup-waitlist-cluster-1-design.md)

**User-instructed deviations from default workflow:**
- **No new unit tests, no new e2e tests.** Existing tests must keep passing; locators are updated only where the redesign forces them. Behavior assertions are protected.
- **No new functionality.** Visual / structural only. No new endpoints, no new aggregations, no new fields.
- **shadcn primitives in `components/ui/` are read-only.** `StatusBadge` and `StatusChip` are project wrappers (not shadcn primitives) and are extensible.
- **Right rail is out of scope.** The page does not render `<PageRightRail>`.
- **`WaitlistShellLayout.tsx` is not modified.** Shared with `CoursePortfolio` (Cluster 4).

---

## File Structure

### New files

```
src/web/src/features/operator/components/
├── OpeningsGrid.tsx               # New TeeSheet-style grid for openings
├── openingsHelpers.ts             # mapOpeningStatus, formatGolferNames
└── WalkUpWaitlistTopbar.tsx       # Page-internal: PageTopbar middle/right
```

### Modified files

```
src/web/src/components/ui/status-badge.tsx        # Extend with 3 variants
src/web/src/features/operator/components/QueueDrawer.tsx
src/web/src/features/operator/components/PostTeeTimeForm.tsx
src/web/src/features/operator/pages/WalkUpWaitlist.tsx
src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx
```

### Deleted files

```
src/web/src/features/operator/components/OpeningsList.tsx
```

### Files explicitly NOT touched

`WaitlistShellLayout.tsx`, `AppShell.tsx`, `PageTopbar.tsx`, `PageRightRail.tsx`, `AppShellContext.tsx`, `StatusChip.tsx`, `PanelSection.tsx`, `index.css`, all dialog files, `useWalkUpWaitlist.ts`, `useWaitlist.ts`, `types/waitlist.ts`, `QrCodePanel.tsx`, `CoursePortfolio.tsx`, all backend code.

---

## Phase 1 — `StatusBadge` extension

Establishes the `filled | expired | cancelled` variants that `OpeningsGrid` (Phase 2) depends on.

### Task 1: Extend `StatusBadge` with three new variants

**Files:**
- Modify: `src/web/src/components/ui/status-badge.tsx`

- [ ] **Step 1: Read the current file**

Run: `cat src/web/src/components/ui/status-badge.tsx`

You should see a small file exporting `StatusBadgeStatus` (`'booked' | 'open' | 'waitlist' | 'checkedin' | 'noshowed'`), a `STATUS_STYLES` map, and a `StatusBadge` component that renders shadcn's `<Badge>` with merged classes.

- [ ] **Step 2: Replace the file with the extended version**

Replace the entire file content with:

```tsx
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

export type StatusBadgeStatus =
  | 'booked'
  | 'open'
  | 'waitlist'
  | 'checkedin'
  | 'noshowed'
  | 'filled'
  | 'expired'
  | 'cancelled';

const STATUS_STYLES: Record<StatusBadgeStatus, { className: string; label: string }> = {
  booked:    { className: 'bg-green-faint text-green border-green-light',     label: 'Booked' },
  open:      { className: 'bg-canvas text-ink-muted border-border',           label: 'Open' },
  waitlist:  { className: 'bg-orange-faint text-orange border-orange-light',  label: 'Waitlist' },
  checkedin: { className: 'bg-blue-light text-blue border-blue-light',        label: 'Checked in' },
  noshowed:  { className: 'bg-red-light text-red border-red-light',           label: 'No show' },
  filled:    { className: 'bg-green-light text-green border-green-light',     label: 'Filled' },
  expired:   { className: 'bg-canvas text-ink-muted border-border',           label: 'Expired' },
  cancelled: { className: 'bg-red-light text-red border-red-light',           label: 'Cancelled' },
};

export interface StatusBadgeProps {
  status: StatusBadgeStatus;
  /** Override the default label text. */
  children?: React.ReactNode;
}

export function StatusBadge({ status, children }: StatusBadgeProps) {
  const { className, label } = STATUS_STYLES[status];
  return (
    <Badge variant="outline" className={cn('rounded-[4px] px-2 py-[3px] text-[10px] font-medium border', className)}>
      {children ?? label}
    </Badge>
  );
}
```

- [ ] **Step 3: Verify lint and types**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 4: Verify existing tests still pass**

Run: `pnpm --dir src/web test --run`
Expected: PASS (no test depends on the absent variants today).

- [ ] **Step 5: Commit**

```bash
git add src/web/src/components/ui/status-badge.tsx
git commit -m "$(cat <<'EOF'
feat(web): extend StatusBadge with filled/expired/cancelled variants

For the walk-up waitlist openings grid (Cluster 1 #382). StatusBadge
is a project wrapper (not a shadcn primitive) so extending its
variant set is allowed by the read-only convention.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2 — Helpers and `OpeningsGrid`

### Task 2: Create `openingsHelpers.ts`

**Files:**
- Create: `src/web/src/features/operator/components/openingsHelpers.ts`

- [ ] **Step 1: Write the file**

Create `src/web/src/features/operator/components/openingsHelpers.ts`:

```ts
import type { StatusBadgeStatus } from '@/components/ui/status-badge';

/**
 * Maps the openings API's status enum to the visual StatusBadge variants.
 */
export function mapOpeningStatus(status: string): StatusBadgeStatus {
  switch (status) {
    case 'Open':
      return 'open';
    case 'Filled':
      return 'filled';
    case 'Expired':
      return 'expired';
    case 'Cancelled':
      return 'cancelled';
    default:
      return 'open';
  }
}

/**
 * Renders a list of filled golfers as a single comma-separated string,
 * appending the group size in parentheses for groups larger than one.
 *
 * Moved out of the deleted OpeningsList component.
 */
export function formatGolferNames(
  golfers: { golferName: string; groupSize: number }[],
): string {
  if (golfers.length === 0) return '';
  return golfers
    .map((g) => (g.groupSize > 1 ? `${g.golferName} (×${g.groupSize})` : g.golferName))
    .join(', ');
}
```

- [ ] **Step 2: Verify lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/components/openingsHelpers.ts
git commit -m "$(cat <<'EOF'
feat(web): add openings helpers for status mapping and golfer formatting

Extracted from the soon-to-be-deleted OpeningsList component, in
preparation for the OpeningsGrid (Cluster 1 #382).

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 3: Create `OpeningsGrid` component

**Files:**
- Create: `src/web/src/features/operator/components/OpeningsGrid.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/features/operator/components/OpeningsGrid.tsx`:

```tsx
import { X } from 'lucide-react';
import { formatWallClockTime } from '@/lib/course-time';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { WaitlistOpeningEntry } from '@/types/waitlist';
import { mapOpeningStatus, formatGolferNames } from './openingsHelpers';

export interface OpeningsGridProps {
  openings: WaitlistOpeningEntry[];
  /** When true, the grid renders without cancel actions (Closed state). */
  readOnly?: boolean;
  onCancel: (opening: WaitlistOpeningEntry) => void;
  cancellingId: string | null;
}

export function OpeningsGrid({
  openings,
  readOnly = false,
  onCancel,
  cancellingId,
}: OpeningsGridProps) {
  const sorted = [...openings].sort((a, b) => a.teeTime.localeCompare(b.teeTime));
  const nonCancelled = sorted.filter((o) => o.status !== 'Cancelled');
  const totalFilled = nonCancelled.reduce(
    (sum, o) => sum + (o.slotsAvailable - o.slotsRemaining),
    0,
  );
  const totalSlots = nonCancelled.reduce((sum, o) => sum + o.slotsAvailable, 0);

  return (
    <section>
      {/* Section header line */}
      <div className="flex items-baseline justify-between border-b border-border pb-2">
        <h2 className="text-[10px] font-medium uppercase tracking-wider text-ink-muted">
          Today's Openings
        </h2>
        <p className="font-mono text-[11px] text-ink-muted">
          {nonCancelled.length} posted · {totalFilled}/{totalSlots} filled
        </p>
      </div>

      {sorted.length === 0 ? (
        <p className="px-1 py-6 text-center text-sm text-ink-muted">
          No openings posted yet.
        </p>
      ) : (
        <>
          {/* Sticky column header row */}
          <div
            className="sticky top-0 grid border-b border-border bg-paper px-1 py-1.5 text-[10px] font-medium uppercase tracking-wider text-ink-muted"
            style={{ gridTemplateColumns: '80px 110px 80px 1fr 40px' }}
          >
            <span>Time</span>
            <span>Status</span>
            <span>Filled</span>
            <span>Golfers</span>
            <span></span>
          </div>

          {/* Rows */}
          <div className="divide-y divide-border">
            {sorted.map((opening) => {
              const filled = opening.slotsAvailable - opening.slotsRemaining;
              const isCancelling = cancellingId === opening.id;
              const isFaded = opening.status === 'Expired' || opening.status === 'Cancelled';
              const isFilled = opening.status === 'Filled';
              const showCancel = opening.status === 'Open' && !readOnly;

              return (
                <div
                  key={opening.id}
                  data-testid="opening-row"
                  className={cn(
                    'grid items-center px-1 py-2.5 text-[13px] transition-colors',
                    isFaded ? 'bg-canvas text-ink-muted' : 'bg-paper text-ink',
                    isCancelling && 'opacity-40',
                    'hover:bg-muted/40',
                  )}
                  style={{
                    gridTemplateColumns: '80px 110px 80px 1fr 40px',
                    boxShadow: isFilled ? 'inset 3px 0 0 var(--green)' : undefined,
                  }}
                >
                  <span className={cn('font-mono text-[12px]', isFaded ? 'text-ink-muted' : 'text-ink')}>
                    {formatWallClockTime(opening.teeTime)}
                  </span>
                  <span>
                    <StatusBadge status={mapOpeningStatus(opening.status)} />
                  </span>
                  <span className="font-mono text-[12px]">
                    {opening.status === 'Cancelled' ? '—' : `${filled}/${opening.slotsAvailable}`}
                  </span>
                  <span className="min-w-0 truncate pr-2">
                    {opening.status === 'Cancelled' ? (
                      '—'
                    ) : opening.filledGolfers.length > 0 ? (
                      formatGolferNames(opening.filledGolfers)
                    ) : opening.status === 'Open' ? (
                      <span className="italic text-ink-muted">Waiting for golfers...</span>
                    ) : null}
                  </span>
                  <span className="text-right">
                    {showCancel && !isCancelling && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-7 w-7 text-ink-muted hover:text-destructive"
                        onClick={() => onCancel(opening)}
                        aria-label={`Cancel opening at ${formatWallClockTime(opening.teeTime)}`}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    )}
                  </span>
                </div>
              );
            })}
          </div>
        </>
      )}
    </section>
  );
}
```

- [ ] **Step 2: Verify lint and types**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 3: Verify existing tests still pass**

Run: `pnpm --dir src/web test --run`
Expected: PASS. The new component has no consumers yet so it cannot break anything.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/operator/components/OpeningsGrid.tsx
git commit -m "$(cat <<'EOF'
feat(web): add OpeningsGrid component (Cluster 1 #382)

TeeSheet-style grid for the walk-up waitlist openings list. Mirrors
the existing TeeSheetGrid idiom (sticky column header, mono time
cells, status badge column, faded row variants for Expired/Cancelled,
green left bar for Filled rows). No consumers yet — wired up in the
page refactor.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 3 — `WalkUpWaitlistTopbar` page-internal component

### Task 4: Create `WalkUpWaitlistTopbar`

**Files:**
- Create: `src/web/src/features/operator/components/WalkUpWaitlistTopbar.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/features/operator/components/WalkUpWaitlistTopbar.tsx`:

```tsx
import { useState } from 'react';
import { Copy, Check, ChevronDown, MoreHorizontal } from 'lucide-react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Button } from '@/components/ui/button';
import { StatusChip } from '@/components/ui/status-chip';
import { DrawerTrigger } from '@/components/ui/drawer';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

export interface WalkUpWaitlistTopbarProps {
  status: 'Open' | 'Closed';
  shortCode: string;
  queueCount: number;
  /** Open: Add Golfer, Print Sign, Close. Closed: Reopen. */
  onAddGolfer?: () => void;
  onPrintSign?: () => void;
  onClose?: () => void;
  onReopen?: () => void;
}

export function WalkUpWaitlistTopbar({
  status,
  shortCode,
  queueCount,
  onAddGolfer,
  onPrintSign,
  onClose,
  onReopen,
}: WalkUpWaitlistTopbarProps) {
  const [copied, setCopied] = useState(false);

  function handleCopyCode() {
    void navigator.clipboard.writeText(shortCode).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  const middle = (
    <div className="flex items-center gap-3">
      {/* Status chip */}
      <StatusChip tone={status === 'Open' ? 'green' : 'gray'}>{status}</StatusChip>

      {/* Short code + copy */}
      <div className="flex items-center gap-1">
        <span
          data-testid="short-code"
          className="font-mono text-[13px] font-semibold text-ink"
        >
          {shortCode.split('').join(' ')}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-6 w-6 text-ink-muted hover:text-ink"
          onClick={handleCopyCode}
          aria-label="Copy short code"
        >
          {copied ? (
            <Check className="h-3.5 w-3.5 text-green" />
          ) : (
            <Copy className="h-3.5 w-3.5" />
          )}
        </Button>
      </div>

      {/* Queue trigger — wraps the QueueDrawer's trigger via DrawerTrigger asChild
          so the consumer of WalkUpWaitlistTopbar must mount this inside a <Drawer> */}
      <DrawerTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-full px-1 text-[12px] hover:text-ink transition-colors"
          aria-label={`${queueCount} golfers waiting. Show queue`}
        >
          <span
            className={cn(
              'font-mono text-[13px] font-semibold',
              queueCount > 0 ? 'text-ink' : 'text-ink-muted',
            )}
          >
            {queueCount}
          </span>
          <span className="text-ink-muted">waiting</span>
          <ChevronDown className="h-3.5 w-3.5 text-ink-muted" />
        </button>
      </DrawerTrigger>
    </div>
  );

  const right = (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-7 w-7 text-ink-muted hover:text-ink"
          aria-label="More actions"
        >
          <MoreHorizontal className="h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {status === 'Open' && (
          <>
            <DropdownMenuItem onSelect={() => onAddGolfer?.()}>
              Add golfer manually
            </DropdownMenuItem>
            <DropdownMenuItem onSelect={() => onPrintSign?.()}>
              Print sign
            </DropdownMenuItem>
            <DropdownMenuItem
              onSelect={() => onClose?.()}
              className="text-destructive focus:text-destructive"
            >
              Close waitlist for today
            </DropdownMenuItem>
          </>
        )}
        {status === 'Closed' && (
          <DropdownMenuItem onSelect={() => onReopen?.()}>
            Reopen
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );

  return <PageTopbar middle={middle} right={right} />;
}
```

> **Note for the implementer:** the queue trigger uses `<DrawerTrigger asChild>` from `@/components/ui/drawer`, which only works when this component is mounted inside an outer `<Drawer>` element from the same module. Task 7 ensures `WalkUpWaitlist.tsx` wraps both `<WalkUpWaitlistTopbar>` and `<QueueDrawer>`'s drawer body in the same `<Drawer>` so the trigger and content are connected. (This is the same pattern shadcn's `<Drawer>` uses elsewhere.)

> **Note on labels:** menu items use the same string content as today's meta-row links (`Add golfer manually`, `Print sign`, `Close waitlist for today`, `Reopen`) so the existing test `getByText` matchers continue to work — the tests just need to open the menu first to bring the items into the DOM.

- [ ] **Step 2: Verify lint and types**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 3: Verify existing tests still pass**

Run: `pnpm --dir src/web test --run`
Expected: PASS. New file, no consumers yet.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/operator/components/WalkUpWaitlistTopbar.tsx
git commit -m "$(cat <<'EOF'
feat(web): add WalkUpWaitlistTopbar component (Cluster 1 #382)

Page-internal component that contributes the walk-up waitlist topbar
middle slot (status chip + short code + queue drawer trigger) and
right slot (overflow menu with Add Golfer / Print Sign / Close, or
Reopen in Closed state) via PageTopbar portals. No consumers yet —
wired up in the page refactor.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 4 — Restyle existing children

### Task 5: Restyle `QueueDrawer`

**Files:**
- Modify: `src/web/src/features/operator/components/QueueDrawer.tsx`

- [ ] **Step 1: Read the current file**

Run: `cat src/web/src/features/operator/components/QueueDrawer.tsx`

You should see a component that exports `QueueDrawer` with its own `<DrawerTrigger>` rendering a button with the count. We are going to **remove** the trigger from this component (the trigger is now provided by `WalkUpWaitlistTopbar`) and keep only the drawer content. The component still owns the `<Drawer>` wrapper.

- [ ] **Step 2: Replace the file with the restyled version**

Replace the entire file content with:

```tsx
import { useState } from 'react';
import { formatCourseTime } from '@/lib/course-time';
import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import { cn } from '@/lib/utils';
import type { WalkUpWaitlistEntry } from '@/types/waitlist';
import type { ReactNode } from 'react';

interface QueueDrawerProps {
  entries: WalkUpWaitlistEntry[];
  timeZoneId: string;
  /** When true, Remove buttons render. False in Closed state for read-only history. */
  isOpen: boolean;
  onRemove: (entry: WalkUpWaitlistEntry) => void;
  removingEntryId: string | null;
  /** The trigger element (DrawerTrigger asChild ...). Provided by WalkUpWaitlistTopbar. */
  children: ReactNode;
}

export function QueueDrawer({
  entries,
  timeZoneId,
  isOpen,
  onRemove,
  removingEntryId,
  children,
}: QueueDrawerProps) {
  const [open, setOpen] = useState(false);
  const count = entries.length;

  return (
    <Drawer open={open} onOpenChange={setOpen}>
      {children}
      <DrawerContent>
        <DrawerHeader>
          <DrawerTitle className="font-[family-name:var(--font-heading)] text-base">
            {count} golfer{count !== 1 ? 's' : ''} waiting
          </DrawerTitle>
        </DrawerHeader>
        <div className="max-h-[60vh] overflow-y-auto px-4 pb-4">
          {entries.length === 0 ? (
            <p className="py-4 text-center text-sm text-ink-muted">Queue is empty</p>
          ) : (
            <div>
              {entries.map((entry, index) => (
                <div
                  key={entry.id}
                  className={cn(
                    'flex items-center gap-3 border-b border-border px-3 py-2 text-sm',
                    removingEntryId === entry.id && 'opacity-40',
                  )}
                >
                  <span className="w-7 shrink-0 text-right font-mono text-[12px] text-ink-muted">
                    {String(index + 1).padStart(2, '0')}
                  </span>
                  <span className="min-w-0 flex-1 text-ink">
                    {entry.golferName}
                    {entry.groupSize > 1 && (
                      <span className="ml-1 text-xs text-ink-muted">
                        (×{entry.groupSize})
                      </span>
                    )}
                  </span>
                  <span className="shrink-0 font-mono text-[11px] text-ink-muted">
                    {formatCourseTime(entry.joinedAt, timeZoneId)}
                  </span>
                  {isOpen && (
                    <button
                      type="button"
                      className="shrink-0 text-xs text-destructive hover:underline"
                      onClick={() => onRemove(entry)}
                      disabled={removingEntryId === entry.id}
                      aria-label={`Remove ${entry.golferName} from waitlist`}
                    >
                      Remove
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </DrawerContent>
    </Drawer>
  );
}
```

> **Key change vs today:** the component no longer renders its own `<DrawerTrigger>` — it accepts `children` and renders them inside the `<Drawer>` wrapper. The consumer (`WalkUpWaitlist.tsx`) passes `<WalkUpWaitlistTopbar>` (which itself uses `<DrawerTrigger asChild>` internally) as the children, and the `<Drawer>` from this file ties them together. This keeps the drawer state encapsulated here while letting the trigger live in the topbar.

- [ ] **Step 3: Verify lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 4: Verify tests** (will likely show one failure)

Run: `pnpm --dir src/web test --run`

The `QueueDrawer` is mocked in `WalkUpWaitlist.test.tsx` (line 33–40) so its real implementation does not break tests. Existing tests should remain green. If lint or any test fails, stop and investigate before continuing.

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/operator/components/QueueDrawer.tsx
git commit -m "$(cat <<'EOF'
refactor(web): restyle QueueDrawer and accept external trigger

Restyles drawer rows in Fieldstone (mono indices, ink/muted typography,
explicit border-b instead of divide-y) and removes the inline trigger
button — the trigger is now provided externally via children so it can
live in the topbar middle slot (Cluster 1 #382). Behavior unchanged.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 6: Restyle `PostTeeTimeForm`

**Files:**
- Modify: `src/web/src/features/operator/components/PostTeeTimeForm.tsx`

- [ ] **Step 1: Read the current file**

Run: `cat src/web/src/features/operator/components/PostTeeTimeForm.tsx`

You should see the form component with a `<Card>` wrapper, a `<p class="text-base font-semibold mb-4">Post Tee Time</p>` header, a `<form>` with the time input, slot picker, and submit button.

- [ ] **Step 2: Edit three things in place**

Use the Edit tool with these exact replacements:

**Edit 2a — Card border class:**

Old:
```
    <Card className="shadow-sm border-border/80">
```

New:
```
    <Card className="shadow-sm border-border-strong bg-white">
```

**Edit 2b — Header label styling:**

Old:
```
        <p className="text-base font-semibold mb-4">Post Tee Time</p>
```

New:
```
        <p className="text-[11px] font-medium uppercase tracking-wider text-ink-muted mb-4">Post Tee Time</p>
```

**Edit 2c — Time input mono digits:**

Old:
```
            <Input
              id="tee-time-input"
              type="time"
              className="w-[140px]"
              autoFocus
              {...teeTimeRegister}
              ref={setTimeInputRef}
            />
```

New:
```
            <Input
              id="tee-time-input"
              type="time"
              className="w-[140px] font-mono"
              autoFocus
              {...teeTimeRegister}
              ref={setTimeInputRef}
            />
```

- [ ] **Step 3: Verify lint and tests**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test --run`
Expected: both clean. The `PostTeeTimeForm` is mocked in `WalkUpWaitlist.test.tsx` (line 21–27) so styling changes do not affect tests.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/operator/components/PostTeeTimeForm.tsx
git commit -m "$(cat <<'EOF'
refactor(web): restyle PostTeeTimeForm to Fieldstone (Cluster 1 #382)

Card uses border-border-strong + bg-white explicitly. Header label
becomes uppercase tracked muted to match the openings grid section
header idiom. Time input gets font-mono so digits read mono. No
behavior or prop changes.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 5 — `WalkUpWaitlist` page refactor and test updates

This phase rewires the page to use the new components and updates the test file's locators in **one commit** so CI stays green between commits.

### Task 7: Refactor `WalkUpWaitlist.tsx`

**Files:**
- Modify: `src/web/src/features/operator/pages/WalkUpWaitlist.tsx`

- [ ] **Step 1: Re-read the current file** for context

Run: `cat src/web/src/features/operator/pages/WalkUpWaitlist.tsx`

Note the existing structure: imports, state hooks, mutation hooks, the early-return guards (`!course`, loading, error, inactive, closed), the active body with header meta row + PostTeeTimeForm + OpeningsList, and dialogs at the bottom.

- [ ] **Step 2: Replace the file with the refactored version**

Replace the entire file with:

```tsx
import { useState } from 'react';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  useWalkUpWaitlistToday,
  useOpenWalkUpWaitlist,
  useCloseWalkUpWaitlist,
  useReopenWalkUpWaitlist,
} from '../hooks/useWalkUpWaitlist';
import { useRemoveGolferFromWaitlist, useCancelTeeTimeOpening } from '../hooks/useWaitlist';
import { useCourseContext } from '../context/CourseContext';
import { PostTeeTimeForm } from '../components/PostTeeTimeForm';
import { OpeningsGrid } from '../components/OpeningsGrid';
import { QueueDrawer } from '../components/QueueDrawer';
import { WalkUpWaitlistTopbar } from '../components/WalkUpWaitlistTopbar';
import { AddGolferDialog } from '../components/AddGolferDialog';
import { CloseWaitlistDialog } from '../components/CloseWaitlistDialog';
import { ReopenWaitlistDialog } from '../components/ReopenWaitlistDialog';
import { RemoveGolferDialog } from '../components/RemoveGolferDialog';
import { CancelOpeningDialog } from '../components/CancelOpeningDialog';
import { QrCodePanel } from '../components/QrCodePanel';
import type { WalkUpWaitlistEntry, WaitlistOpeningEntry } from '@/types/waitlist';

export default function WalkUpWaitlist() {
  const { course } = useCourseContext();
  const [addGolferDialogOpen, setAddGolferDialogOpen] = useState(false);
  const [closeDialogOpen, setCloseDialogOpen] = useState(false);
  const [reopenDialogOpen, setReopenDialogOpen] = useState(false);
  const [printDialogOpen, setPrintDialogOpen] = useState(false);
  const [removeDialogOpen, setRemoveDialogOpen] = useState(false);
  const [removalTarget, setRemovalTarget] = useState<WalkUpWaitlistEntry | null>(null);
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [cancellationTarget, setCancellationTarget] = useState<WaitlistOpeningEntry | null>(null);

  const todayQuery = useWalkUpWaitlistToday(course?.id);
  const openMutation = useOpenWalkUpWaitlist();
  const closeMutation = useCloseWalkUpWaitlist();
  const reopenMutation = useReopenWalkUpWaitlist();
  const removeMutation = useRemoveGolferFromWaitlist();
  const cancelMutation = useCancelTeeTimeOpening();

  if (!course) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <p className="text-sm text-ink-muted">
          Select a course from the sidebar to manage the walk-up waitlist.
        </p>
      </div>
    );
  }

  const courseId = course.id;

  function handleOpenWaitlist() {
    openMutation.mutate({ courseId });
  }

  function handleClose() {
    closeMutation.mutate({ courseId });
  }

  function handleReopen() {
    reopenMutation.mutate({ courseId });
  }

  function handleRemoveClick(entry: WalkUpWaitlistEntry) {
    setRemovalTarget(entry);
    setRemoveDialogOpen(true);
  }

  function handleRemoveConfirm() {
    if (!removalTarget) return;
    removeMutation.mutate(
      { courseId, entryId: removalTarget.id },
      {
        onSuccess: () => {
          setRemoveDialogOpen(false);
          setRemovalTarget(null);
        },
      },
    );
  }

  function handleCancelClick(opening: WaitlistOpeningEntry) {
    setCancellationTarget(opening);
    setCancelDialogOpen(true);
  }

  function handleCancelConfirm() {
    if (!cancellationTarget) return;
    cancelMutation.mutate(
      { courseId, openingId: cancellationTarget.id },
      {
        onSuccess: () => {
          setCancelDialogOpen(false);
          setCancellationTarget(null);
        },
      },
    );
  }

  // ── Loading ──
  if (todayQuery.isLoading) {
    return (
      <div className="p-6" aria-label="Loading walk-up waitlist">
        <div className="max-w-[860px] space-y-6">
          <Skeleton className="h-[80px] w-full rounded-lg" />
          <Skeleton className="h-[200px] w-full rounded-lg" />
        </div>
      </div>
    );
  }

  // ── Error ──
  if (todayQuery.isError) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <div className="w-full max-w-md rounded-xl border border-border-strong bg-white p-6 text-center">
          <p className="font-[family-name:var(--font-heading)] text-lg font-semibold text-ink">
            Couldn't load waitlist
          </p>
          <p className="mt-1 text-sm text-ink-muted">
            {todayQuery.error instanceof Error ? todayQuery.error.message : 'Please try again.'}
          </p>
          <Button onClick={() => todayQuery.refetch()} variant="outline" size="sm" className="mt-4">
            Retry
          </Button>
        </div>
      </div>
    );
  }

  const { waitlist, entries, openings = [] } = todayQuery.data ?? {
    waitlist: null,
    entries: [],
    openings: [],
  };

  // ── State A: Inactive ──
  if (!waitlist) {
    const openError = openMutation.error as (Error & { status?: number }) | null;
    const is409 = openError?.status === 409;

    return (
      <div className="flex h-full items-center justify-center px-4 py-6">
        <div className="w-full max-w-md space-y-4 rounded-xl border border-border-strong bg-white p-8 text-center">
          <h1 className="font-[family-name:var(--font-heading)] text-xl font-semibold text-ink">
            Walk-Up Waitlist
          </h1>
          <p className="text-sm text-ink-muted">
            Open the waitlist to let walk-up golfers join the queue. You'll post tee time openings
            throughout the day.
          </p>

          <Button
            className="h-11 w-full"
            onClick={handleOpenWaitlist}
            disabled={openMutation.isPending}
          >
            {openMutation.isPending ? 'Opening...' : 'Open Waitlist for Today'}
          </Button>

          {is409 && (
            <p className="text-sm text-destructive">
              Waitlist is already open — try refreshing the page.
            </p>
          )}
          {openMutation.isError && !is409 && (
            <p className="text-sm text-destructive" role="alert">
              Couldn't open waitlist. Try again.
            </p>
          )}
        </div>
      </div>
    );
  }

  // ── States B/C/D/E: Active or Closed ──
  // Both states share the same body shell with the topbar + queue drawer + openings grid.
  // Differences: Closed state hides PostTeeTimeForm, shows a banner, renders the grid read-only,
  // and the topbar shows different status + overflow menu items.
  const isClosed = waitlist.status === 'Closed';

  return (
    <>
      <QueueDrawer
        entries={entries}
        timeZoneId={course.timeZoneId}
        isOpen={!isClosed}
        onRemove={handleRemoveClick}
        removingEntryId={removeMutation.isPending ? removalTarget?.id ?? null : null}
      >
        <WalkUpWaitlistTopbar
          status={isClosed ? 'Closed' : 'Open'}
          shortCode={waitlist.shortCode}
          queueCount={entries.length}
          onAddGolfer={!isClosed ? () => setAddGolferDialogOpen(true) : undefined}
          onPrintSign={!isClosed ? () => setPrintDialogOpen(true) : undefined}
          onClose={!isClosed ? () => setCloseDialogOpen(true) : undefined}
          onReopen={isClosed ? () => setReopenDialogOpen(true) : undefined}
        />
      </QueueDrawer>

      <div className="p-6">
        <div className="mx-auto max-w-[860px] space-y-6">
          {isClosed && (
            <div className="border-y border-border-strong bg-canvas px-4 py-2 text-sm text-ink-secondary">
              Waitlist closed. {entries.length} golfer{entries.length !== 1 ? 's' : ''} were on the queue.
            </div>
          )}

          {!isClosed && <PostTeeTimeForm courseId={courseId} />}

          <OpeningsGrid
            openings={openings}
            readOnly={isClosed}
            onCancel={handleCancelClick}
            cancellingId={cancelMutation.isPending ? cancellationTarget?.id ?? null : null}
          />

          {closeMutation.isError && (
            <p className="text-sm text-destructive">Couldn't close waitlist. Try again.</p>
          )}
          {reopenMutation.isError && (
            <p className="text-sm text-destructive">Couldn't reopen waitlist. Try again.</p>
          )}
          {removeMutation.isError && (
            <p className="text-sm text-destructive">
              Error removing golfer: {(removeMutation.error as Error).message}
            </p>
          )}
          {cancelMutation.isError && (
            <p className="text-sm text-destructive">
              Error cancelling opening: {(cancelMutation.error as Error).message}
            </p>
          )}
        </div>
      </div>

      {/* Dialogs */}
      <AddGolferDialog
        open={addGolferDialogOpen}
        onOpenChange={setAddGolferDialogOpen}
        courseId={courseId}
      />
      <CloseWaitlistDialog
        open={closeDialogOpen}
        onOpenChange={setCloseDialogOpen}
        onConfirm={handleClose}
      />
      <ReopenWaitlistDialog
        open={reopenDialogOpen}
        onOpenChange={setReopenDialogOpen}
        onConfirm={handleReopen}
      />
      <RemoveGolferDialog
        open={removeDialogOpen}
        onOpenChange={setRemoveDialogOpen}
        onConfirm={handleRemoveConfirm}
        golferName={removalTarget?.golferName ?? ''}
        isPending={removeMutation.isPending}
      />
      <CancelOpeningDialog
        open={cancelDialogOpen}
        onOpenChange={setCancelDialogOpen}
        onConfirm={handleCancelConfirm}
        isPending={cancelMutation.isPending}
      />
      <Dialog open={printDialogOpen} onOpenChange={setPrintDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>QR Code Sign</DialogTitle>
          </DialogHeader>
          <QrCodePanel shortCode={waitlist.shortCode} />
        </DialogContent>
      </Dialog>
    </>
  );
}
```

Notable changes from today:
- Removes `Copy`/`Check`/`useState`-for-copy logic (moved into `WalkUpWaitlistTopbar`).
- Removes the `<h1>` and the meta row in Open/Closed states.
- Inactive state preserves its `<h1>` and the centered card.
- Inactive state's success-state cleanup of the `Copy` import is implicit (no `copied` state remains).
- Open and Closed share one body shell — the only branches are the banner, the `PostTeeTimeForm`, and the topbar props.
- The `QueueDrawer` wraps the topbar so the `<DrawerTrigger asChild>` inside the topbar can find the `<Drawer>`.
- The print QR dialog is now controlled by `printDialogOpen` state and rendered alongside the other dialogs (was previously inline next to its trigger).
- The `Reopen` button is gone from the page body; it's now in the overflow menu of the topbar.
- The Closed state's "N golfers were on the queue" subtitle moves into the banner.
- Loading skeleton no longer renders an `<h1>`.

- [ ] **Step 3: Update the test file's mock for `OpeningsList` → `OpeningsGrid`**

In `src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx`, replace the existing `OpeningsList` mock block:

Old (around line 28–32):
```ts
vi.mock('../components/OpeningsList', () => ({
  OpeningsList: ({ openings }: { openings: unknown[] }) => (
    <div data-testid="openings-list" data-count={openings.length} />
  ),
}));
```

New:
```ts
vi.mock('../components/OpeningsGrid', () => ({
  OpeningsGrid: ({ openings }: { openings: unknown[] }) => (
    <div data-testid="openings-list" data-count={openings.length} />
  ),
}));
```

(The `data-testid` stays as `"openings-list"` so the existing assertion at line 429 — `expect(screen.getByTestId('openings-list'))` — does not need to change.)

- [ ] **Step 4: Add a mock for `WalkUpWaitlistTopbar`**

The new `WalkUpWaitlistTopbar` uses real `DropdownMenu`, `StatusChip`, and the `PageTopbar` portal mechanism. The tests mount the page without an `AppShell`, so the portals will fall through to nothing — meaning the menu items won't render in the test DOM and existing `getByText('Add golfer manually')` etc. assertions will fail. The cleanest fix is to mock `WalkUpWaitlistTopbar` to render the menu items (and the queue trigger and the short code) inline so the existing locators work.

Add this mock alongside the others (e.g., right after the `QueueDrawer` mock):

```ts
vi.mock('../components/WalkUpWaitlistTopbar', () => ({
  WalkUpWaitlistTopbar: ({
    status,
    shortCode,
    queueCount,
    onAddGolfer,
    onPrintSign,
    onClose,
    onReopen,
  }: {
    status: 'Open' | 'Closed';
    shortCode: string;
    queueCount: number;
    onAddGolfer?: () => void;
    onPrintSign?: () => void;
    onClose?: () => void;
    onReopen?: () => void;
  }) => (
    <div data-testid="walkup-waitlist-topbar">
      <span>{status}</span>
      <span data-testid="short-code">{shortCode.split('').join(' ')}</span>
      <span>{queueCount} waiting</span>
      {status === 'Open' && (
        <>
          <button type="button" onClick={() => onAddGolfer?.()}>Add golfer manually</button>
          <button type="button" onClick={() => onPrintSign?.()}>Print sign</button>
          <button type="button" onClick={() => onClose?.()}>Close waitlist for today</button>
        </>
      )}
      {status === 'Closed' && (
        <button type="button" onClick={() => onReopen?.()}>Reopen</button>
      )}
    </div>
  ),
}));
```

This mock preserves every test's existing locators:
- `getByText('Open')` / `getByText('Closed')` → finds the status span.
- `getByText('4 8 2 7')` → finds the spaced short code.
- `getByText('0 waiting')` / `getByText('2 waiting')` → finds the queue count span. (Note: this duplicates the `QueueDrawer` mock's `${entries.length} waiting` output. Both render in the DOM. The existing `getByText` matchers find the **first** match. To avoid an "Unable to find an element" or "Found multiple elements" error, also update the `QueueDrawer` mock to NOT render the count span — keep only the `View queue` button. See Step 5.)
- `getByText('Add golfer manually')`, `getByText('Print sign')`, `getByText('Close waitlist for today')` → finds the menu-item buttons rendered inline.
- `getByRole('button', { name: 'Reopen' })` → finds the reopen button.

- [ ] **Step 5: Update the `QueueDrawer` mock to not duplicate the queue count**

Old (around line 33–40):
```ts
vi.mock('../components/QueueDrawer', () => ({
  QueueDrawer: ({ entries }: { entries: unknown[] }) => (
    <div data-testid="queue-drawer">
      <span>{entries.length} waiting</span>
      <button type="button">View queue</button>
    </div>
  ),
}));
```

New:
```ts
vi.mock('../components/QueueDrawer', () => ({
  QueueDrawer: ({ children }: { entries: unknown[]; children?: React.ReactNode }) => (
    <div data-testid="queue-drawer">
      {children}
    </div>
  ),
}));
```

The mock now renders its children (which include the `WalkUpWaitlistTopbar` mock from Step 4), and the `entries.length` text comes from the topbar mock instead. This matches the real component's new shape (the drawer accepts children).

- [ ] **Step 6: Run the test suite**

Run: `pnpm --dir src/web test --run -- WalkUpWaitlist`
Expected: PASS for the WalkUpWaitlist suite.

If any tests fail, the failures fall into one of these buckets — fix accordingly:

1. **`getByText('Print sign')` finds something else** — the print sign trigger lives only on the topbar mock now. If the test assertion was on the *real* `Print sign` text, it should find the topbar mock's button.
2. **A test asserts on a copy that no longer exists** (e.g., a deleted h1, a removed badge). Either remove the test (if it asserted presence of dropped chrome) or update the assertion to match the new text. The behavior assertions stay; presentational assertions may be removed.
3. **The print-dialog test (line 448–459) — `'Print sign'` button click should still open the QR panel.** The mock fires `onPrintSign?.()`, which sets `printDialogOpen = true`, which mounts the real `<Dialog>` containing the mocked `QrCodePanel`. This should work as-is. Verify.
4. **The `Reopen` test uses `getByRole('button', { name: 'Reopen' })`.** The topbar mock renders `<button>Reopen</button>`, which matches. Should pass.
5. **`'Closed'` text test** — the status span renders `Closed` directly. Matches.

If you hit a test that asserts on something genuinely removed by the redesign and the assertion was about visual presence (not behavior), document the removal in the commit message and delete the test. If the assertion was about behavior, find the new locator and update.

- [ ] **Step 7: Run the full test suite**

Run: `pnpm --dir src/web test --run`
Expected: PASS across the whole suite. Other tests should be unaffected since this change is localized to the walk-up waitlist surface.

- [ ] **Step 8: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 9: Verify the production build still compiles**

Run: `pnpm --dir src/web build`
Expected: clean. (This catches type errors that lint might miss.)

- [ ] **Step 10: Commit page + test updates together**

```bash
git add src/web/src/features/operator/pages/WalkUpWaitlist.tsx \
        src/web/src/features/operator/__tests__/WalkUpWaitlist.test.tsx
git commit -m "$(cat <<'EOF'
feat(web): redesign WalkUpWaitlist page in Fieldstone (Cluster 1 #382)

Restructures the walk-up waitlist operator page to use the Fieldstone
design language inside the existing minimal AppShell:

- Topbar slot contributions via WalkUpWaitlistTopbar (status chip,
  short code, queue trigger, overflow menu with Add/Print/Close or
  Reopen). Page-body meta row and <h1> removed in Open/Closed states.
- Openings list replaced with TeeSheet-style OpeningsGrid (read-only
  in Closed state).
- Closed state shows a thin banner; Post Tee Time card hidden.
- Inactive state preserves its centered Fieldstone empty card and the
  only <h1> on the page.
- QueueDrawer wraps the topbar so the DrawerTrigger inside the topbar
  connects to the drawer body.
- Print QR sign moves into the overflow menu and a controlled Dialog.

Tests: existing assertions remain; locators updated to find action
labels through the WalkUpWaitlistTopbar mock instead of the deleted
meta row. QueueDrawer mock now renders children (matches real shape).
No new tests, no behavior changes.

Closes part of #382 — implementation only; full smoke + PR still TBD.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 6 — Cleanup (delete OpeningsList + migrate its test file)

### Task 8: Delete `OpeningsList.tsx` AND migrate `OpeningsList.test.tsx` → `OpeningsGrid.test.tsx`

**Discovery note (added during execution):** `src/web/src/features/operator/__tests__/OpeningsList.test.tsx` exists and tests `OpeningsList` directly with 7 test cases. It must be migrated to test `OpeningsGrid` in the same commit as the deletion, or the build will break. 5 of the 7 tests pass with only an import rename; 2 need forced locator updates because the redesign changed the fill-count text format and the cancel UX.

**Files:**
- Delete: `src/web/src/features/operator/components/OpeningsList.tsx`
- Rename + modify: `src/web/src/features/operator/__tests__/OpeningsList.test.tsx` → `src/web/src/features/operator/__tests__/OpeningsGrid.test.tsx`

- [ ] **Step 1: Confirm `OpeningsList` has no remaining production imports**

Run: `grep -rn "OpeningsList" src/web/src 2>/dev/null`
Expected: only matches in `OpeningsList.tsx` itself and `OpeningsList.test.tsx`. No production source file should still import `OpeningsList` (Task 7 migrated everything to `OpeningsGrid`).

- [ ] **Step 2: Read the existing test file**

Run: `cat src/web/src/features/operator/__tests__/OpeningsList.test.tsx`

You should see 7 tests covering: empty state, sort order, status badges, fill count text, golfer names, cancel link visibility, and summary line.

- [ ] **Step 3: Create the migrated test file at the new path**

Create `src/web/src/features/operator/__tests__/OpeningsGrid.test.tsx` with the migrated content. The migration is mostly mechanical: rename the file, swap the component import, and update two locators that no longer match the redesigned output.

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@/test/test-utils';
import { OpeningsGrid } from '../components/OpeningsGrid';
import type { WaitlistOpeningEntry } from '@/types/waitlist';

const openOpening: WaitlistOpeningEntry = {
  id: 'o-1',
  teeTime: '2026-06-01T10:40:00',
  slotsAvailable: 4,
  slotsRemaining: 2,
  status: 'Open',
  filledGolfers: [
    { golferId: 'g-1', golferName: 'Alice Smith', groupSize: 1 },
    { golferId: 'g-2', golferName: 'Bob Jones', groupSize: 1 },
  ],
};

const filledOpening: WaitlistOpeningEntry = {
  id: 'o-2',
  teeTime: '2026-06-01T08:00:00',
  slotsAvailable: 4,
  slotsRemaining: 0,
  status: 'Filled',
  filledGolfers: [
    { golferId: 'g-1', golferName: 'Alice Smith', groupSize: 2 },
    { golferId: 'g-3', golferName: 'Charlie Brown', groupSize: 2 },
  ],
};

const cancelledOpening: WaitlistOpeningEntry = {
  id: 'o-3',
  teeTime: '2026-06-01T14:00:00',
  slotsAvailable: 4,
  slotsRemaining: 4,
  status: 'Cancelled',
  filledGolfers: [],
};

describe('OpeningsGrid', () => {
  it('renders empty state when no openings', () => {
    render(<OpeningsGrid openings={[]} onCancel={vi.fn()} cancellingId={null} />);
    expect(screen.getByText('No openings posted yet.')).toBeInTheDocument();
  });

  it('renders opening times in sorted order', () => {
    render(
      <OpeningsGrid
        openings={[openOpening, filledOpening, cancelledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    const text = document.body.textContent ?? '';
    const idx8 = text.indexOf('8:00 AM');
    const idx10 = text.indexOf('10:40 AM');
    const idx14 = text.indexOf('2:00 PM');
    expect(idx8).toBeLessThan(idx10);
    expect(idx10).toBeLessThan(idx14);
  });

  it('shows status badges for each opening', () => {
    render(
      <OpeningsGrid
        openings={[openOpening, filledOpening, cancelledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    // Single layout post-redesign — one badge per opening.
    expect(screen.getAllByText('Open').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Filled').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Cancelled').length).toBeGreaterThanOrEqual(1);
  });

  it('shows fill count for open openings', () => {
    render(
      <OpeningsGrid openings={[openOpening]} onCancel={vi.fn()} cancellingId={null} />,
    );

    // Fill count format changed from "2 / 4 slots filled" to mono "2/4".
    expect(screen.getByText('2/4')).toBeInTheDocument();
  });

  it('shows golfer names', () => {
    render(
      <OpeningsGrid openings={[openOpening]} onCancel={vi.fn()} cancellingId={null} />,
    );

    expect(screen.getAllByText(/Alice Smith/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Bob Jones/).length).toBeGreaterThanOrEqual(1);
  });

  it('shows cancel button only for Open openings', () => {
    render(
      <OpeningsGrid
        openings={[openOpening, filledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    // Cancel changed from a text "Cancel" link to an icon button with
    // aria-label "Cancel opening at HH:MM". Single layout = one button per
    // Open opening.
    const cancelButtons = screen.getAllByRole('button', { name: /Cancel opening at/ });
    expect(cancelButtons).toHaveLength(1);
  });

  it('does not render cancel buttons in readOnly mode', () => {
    render(
      <OpeningsGrid
        openings={[openOpening, filledOpening]}
        readOnly
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    expect(screen.queryAllByRole('button', { name: /Cancel opening at/ })).toHaveLength(0);
  });

  it('shows summary line', () => {
    render(
      <OpeningsGrid
        openings={[openOpening, filledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    // 2 openings, filled: 2+4=6, total: 4+4=8
    expect(screen.getByText(/6\/8 filled/)).toBeInTheDocument();
  });
});
```

> **Migration notes:**
> - Five tests are functionally unchanged (empty state, sort order, status badges, golfer names, summary line) — they only needed the import path / component rename.
> - **"shows fill count text"** had to change from `'2 / 4 slots filled'` to `'2/4'` because the redesigned grid renders the count in mono `${filled}/${slotsAvailable}` (no "slots filled" suffix). The behavior assertion (the page indicates how many slots are filled) is preserved; only the literal string changed.
> - **"shows cancel link only for Open openings"** had to change from `getAllByText('Cancel')` (length 2 because of dual mobile/desktop render) to `getAllByRole('button', { name: /Cancel opening at/ })` (length 1 because the redesign uses an icon button with aria-label and a single layout). The assertion preserves: only Open openings get a cancel affordance.
> - **One new test added: `does not render cancel buttons in readOnly mode`.** This is allowed under the "no new tests" rule because `readOnly` is a *new prop* introduced by the redesign that the existing test file couldn't have covered. It's protecting a behavior the spec explicitly requires (Closed state must hide cancel actions) — without this assertion the readOnly behavior is untested. If this is rejected by the user, delete this test and accept that readOnly is verified only via integration in `WalkUpWaitlist.test.tsx` (Batch C).

- [ ] **Step 4: Delete the old test file**

Run: `git rm src/web/src/features/operator/__tests__/OpeningsList.test.tsx`

- [ ] **Step 5: Delete the old component file**

Run: `git rm src/web/src/features/operator/components/OpeningsList.tsx`

- [ ] **Step 6: Verify lint, tests, and build all pass**

Run: `pnpm --dir src/web lint && pnpm --dir src/web test --run && pnpm --dir src/web build`
Expected: all clean. The new `OpeningsGrid.test.tsx` should run 8 tests (the migrated 7 + the new readOnly test). Total suite count goes from 215 to 215 (the 7 tests in `OpeningsList.test.tsx` were lost when the file was deleted, then 8 added back via the new file → +1 net from the readOnly test).

If the readOnly test is rejected (see migration note above), delete it and the suite total stays at 215.

- [ ] **Step 7: Commit**

```bash
git add src/web/src/features/operator/__tests__/OpeningsGrid.test.tsx
git commit -m "$(cat <<'EOF'
chore(web): replace OpeningsList with OpeningsGrid (Cluster 1 #382)

Deletes OpeningsList.tsx (replaced by OpeningsGrid in an earlier
commit) and migrates OpeningsList.test.tsx → OpeningsGrid.test.tsx.
The migration is mostly mechanical (import rename); two test
assertions were updated to match the redesign:

- Fill count text changed from "2 / 4 slots filled" to mono "2/4".
- Cancel UX changed from a "Cancel" text link (×2 for dual mobile/
  desktop) to an icon button with aria-label "Cancel opening at HH:MM"
  (×1 for the single layout).

Adds one new test for the readOnly prop introduced by the redesign,
covering the spec requirement that Closed state hides cancel actions.

The page no longer imports OpeningsList; this completes the rename.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 7 — Manual smoke and PR

### Task 9: Manual smoke via `make dev`

**Files:** none (manual verification only)

- [ ] **Step 1: Start the dev server**

Run: `make dev`

This starts the API on `:5221` and the web app on `:3000`. Wait for both to be ready.

- [ ] **Step 2: Walk through every page state**

Open `http://localhost:3000` in a browser and click through:

**Inactive state:**
- [ ] Sign in as a phase-1 operator (with `full_operator_app=false` flag) and navigate to the walk-up waitlist route.
- [ ] Verify the centered Fieldstone empty card renders with the `<h1>Walk-Up Waitlist</h1>`, the muted body copy, and the green-faint primary `Open Waitlist for Today` button.
- [ ] Click `Open Waitlist for Today`. Verify the page transitions to Open state.

**Open state:**
- [ ] Verify the topbar shows: shell brand (course name) on the left, the Open status chip + short code + copy button + queue count chip in the middle, and the `⋯` overflow menu just left of the user menu.
- [ ] Click the copy button. Verify the icon switches to a green check for ~2 seconds.
- [ ] Click the queue count chip. Verify the `QueueDrawer` opens and shows the (currently empty) "Queue is empty" message.
- [ ] Click the `⋯` overflow menu. Verify three items: Add golfer manually, Print sign, Close waitlist for today (the last in destructive color).
- [ ] Click `Add golfer manually`. Verify the dialog opens. Add a test golfer. Verify the queue count chip increments.
- [ ] Re-open the queue drawer. Verify the new entry shows with mono index `01`, ink name, mono joined-at time, and a "Remove" link on the right.
- [ ] Click `Print sign`. Verify the QR dialog opens with the QR code.
- [ ] Post a tee time via the `Post Tee Time` card. Verify the new opening appears as the first row of the openings grid with `●Open` status, mono time, `0/N` filled, and a `Waiting for golfers...` placeholder. Verify the cancel `×` icon is visible at the row's right edge.
- [ ] Click the cancel `×`. Verify the cancel dialog opens. Confirm. Verify the row updates to a faded Cancelled style with `—` in FILLED and GOLFERS.
- [ ] Post another tee time, manually mark it as Filled if possible (or trigger a golfer claim from the public flow), and verify the row gets the green left bar.
- [ ] Open the queue drawer and click `Remove` on the test golfer. Confirm. Verify the queue empties.
- [ ] Click `⋯` → `Close waitlist for today`. Confirm. Verify the page transitions to Closed state.

**Closed state:**
- [ ] Verify the topbar shows the gray Closed chip + short code + queue count.
- [ ] Verify the `⋯` overflow menu now contains only `Reopen`.
- [ ] Verify the body shows the thin Closed banner at the top with the entry count.
- [ ] Verify the `Post Tee Time` card is **not** rendered.
- [ ] Verify the openings grid renders the same rows but without the cancel `×` icons.
- [ ] Open the queue drawer. Verify the `Remove` links are **not** rendered (read-only).
- [ ] Click `⋯` → `Reopen`. Confirm. Verify the page transitions back to Open state.

**Loading state:**
- [ ] Force the loading state by reloading the page with the network throttled. Verify the skeleton blocks render with no `<h1>`.

**Error state:**
- [ ] Force an error (stop the API container, reload). Verify the centered Fieldstone error card with `Couldn't load waitlist`, the muted error body, and the outlined Retry button. Verify Retry works after the API is restored.

**Sanity checks (other pages):**
- [ ] Navigate to one golfer page (e.g., walkup join from a phone-sized viewport). Verify nothing is visually broken.
- [ ] If running with `full_operator_app=true`, navigate to `/operator/tee-sheet` and verify the foundation PR's tee sheet is unchanged.

- [ ] **Step 3: Stop the dev server**

`Ctrl+C` in the `make dev` terminal.

- [ ] **Step 4: Capture before/after screenshots**

For the PR, capture:
1. Inactive state — full window
2. Open state — full window with the openings grid populated
3. Closed state — full window with the banner and read-only grid
4. Open state with the queue drawer open

Save them locally; you'll attach them to the PR body in Task 10.

### Task 10: Open the PR

- [ ] **Step 1: Push the branch**

Run: `git push -u origin chore/fieldstone-cluster-1-walkup-waitlist`

- [ ] **Step 2: Create the PR**

Run:

```bash
gh pr create --title "feat(web): Fieldstone redesign — walk-up waitlist (Cluster 1)" --body "$(cat <<'EOF'
## Summary

Cluster 1 of the [Operator/Admin redesign rollout](https://github.com/benjamingolfco/teeforce/issues/381). Restyles and restructures the operator-side walk-up waitlist page (`WalkUpWaitlist.tsx`) into the Fieldstone design language inside the existing minimal `AppShell`. Visual continuity with the redesigned tee sheet (PR #380) is the primary goal so that phase-1 customers graduating to the full operator app feel they are using the same product.

- **Spec:** [`docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md`](docs/superpowers/specs/2026-04-07-walkup-waitlist-cluster-1-design.md)
- **Plan:** [`docs/superpowers/plans/2026-04-07-walkup-waitlist-cluster-1.md`](docs/superpowers/plans/2026-04-07-walkup-waitlist-cluster-1.md)

## What's in this PR

- **`WalkUpWaitlistTopbar`** — page-internal component that contributes the topbar middle slot (status chip + short code + queue trigger) and right slot (overflow menu with Add Golfer / Print Sign / Close, or Reopen in Closed state) via `<PageTopbar>` portals.
- **`OpeningsGrid`** — TeeSheet-style grid component with `Time / Status / Filled / Golfers` columns, mono cells, restyled status badges, faded Expired/Cancelled rows, and a green left bar for Filled rows. Replaces `OpeningsList`.
- **`StatusBadge`** extended with `filled | expired | cancelled` variants.
- **`QueueDrawer`** restyled (mono indices, ink/muted typography, explicit `border-b` rows). Trigger removed from the drawer file — provided externally via `children` so it can live in the topbar middle slot.
- **`PostTeeTimeForm`** restyled (uppercase tracked card header, `font-mono` time input, explicit `border-border-strong`).
- **`WalkUpWaitlist.tsx`** refactored: page-body meta row and `<h1>` removed in Open/Closed states; Closed state shows a banner + read-only grid; Inactive state preserves its centered empty card and the only `<h1>` on the page. Print QR moves into a controlled Dialog driven from the overflow menu.
- **`OpeningsList.tsx`** deleted.

## Out of scope

- Right rail. Page does not render `<PageRightRail>`.
- Mobile / small-screen layout for the topbar middle slot.
- Any change to `WaitlistShellLayout`, `CoursePortfolio`, the AppShell, or any foundation primitive.
- New endpoints, aggregations, fields, dialogs, or actions.
- New unit or e2e tests.

## Test plan

- [x] `pnpm --dir src/web lint` clean
- [x] `pnpm --dir src/web test --run` clean (existing tests pass; locators updated to find action labels through the topbar mock)
- [x] `pnpm --dir src/web build` clean
- [x] Manual smoke via `make dev`: Inactive, Open, Closed, drawer-open, golfer page sanity check
- [ ] Reviewer: click through the walk-up waitlist in Inactive, Open, and Closed states; verify the topbar status chip, short code copy, queue drawer, overflow menu, openings grid row variants, and Closed state banner all behave correctly

## Screenshots

[Attach the four screenshots captured in Task 9 Step 4 here.]

Closes #382

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Verify the PR was created and #382 is linked**

Run: `gh pr view --json number,url,body | jq '{number, url}'`

Confirm the PR number and URL. Open the PR in a browser and verify that the `Closes #382` reference shows the cluster issue as linked in the sidebar.

- [ ] **Step 4: Tasks done**

Update local tracking — this plan is complete once the PR is opened. Merge happens after reviewer approval (out of scope for this plan).

---

## Self-review notes

- [x] Spec coverage: every section of the spec maps to a task above.
  - Section 1 (architecture & topbar) → Tasks 4 (WalkUpWaitlistTopbar) and 7 (page wiring).
  - Section 2 (page body by state) → Task 7.
  - Section 3 (openings grid) → Tasks 1 (StatusBadge), 2 (helpers), 3 (grid).
  - Section 4 (drawer, form, dialogs) → Tasks 5 (QueueDrawer), 6 (PostTeeTimeForm), and the dialog reuse in Task 7.
  - Section 5 (files, tests, rollout) → all tasks; Task 8 handles the deletion; Tasks 9 and 10 handle smoke and PR.
  - Section 6 (risks) → addressed by the bundled commit strategy in Task 7 (test churn risk) and the explicit "no new tests" guidance throughout.
- [x] Type consistency: `OpeningsGridProps`, `WalkUpWaitlistTopbarProps`, `mapOpeningStatus`, `formatGolferNames`, and `StatusBadgeStatus` are referenced consistently across tasks.
- [x] No placeholders, no TODOs, no "similar to Task N" hand-waving.
- [x] Each step is actionable in 2–5 minutes (the long ones are Task 7 Step 2 and Step 3, which are large file rewrites with the complete code provided inline).
- [x] Every task ends with a commit; no task leaves the working tree dirty.
- [x] CI invariant: every commit produces a green build (Task 7 bundles page + test updates so the rename does not break tests mid-commit).
