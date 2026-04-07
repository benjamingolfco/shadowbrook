# Fieldstone Cluster 3 — Admin System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle three admin system pages (`Dashboard`, `FeatureFlags`, `DeadLetters`) to the Fieldstone design language inside the existing `<AppShell variant="full">` mount, closing #384.

**Architecture:** Visual / structural refactor only. Each page contributes its title via `<PageTopbar>` from inside its render tree, drops its outer `p-6 space-y-6` wrapper, and applies the Cluster 2 panel + table idioms (`border-border-strong`, uppercase tracked `<CardTitle>`, `bg-canvas` table headers, mono cells). Data hooks, mutations, behaviors, and routing are unchanged. No new tests; existing `DeadLetters.test.tsx` locators are updated only where the redesign forces them.

**Tech Stack:** React 19, TypeScript 5.9, Tailwind 4 (`@theme inline` tokens in `index.css`), shadcn/ui (`<Card>`, `<CardHeader>`, `<CardTitle>`, `<CardContent>`, `<Table>`, `<Switch>`, `<AlertDialog>`), recharts, TanStack Query, React Router v7, Vitest + React Testing Library.

**Spec:** [`docs/superpowers/specs/2026-04-08-admin-system-cluster-3-design.md`](../specs/2026-04-08-admin-system-cluster-3-design.md)

---

## Preconditions

- Branch `chore/fieldstone-cluster-3-admin-system` is checked out.
- The spec file is committed on that branch.
- `features/admin/components/StatTile.tsx` exists from Cluster 2 with signature `{ label: string; value: ReactNode }`.
- `src/web/src/test/test-utils.tsx` already wraps render with `TestAppShellProvider` that provides real DOM nodes for topbar slots (verified — content portaled into the topbar is findable via `screen`).
- `app/router.tsx` already mounts `<AppShell variant="full" navConfig={adminNav} brand={adminBrand}>` around `/admin/*` routes.

## File Structure

**Files modified (3):**
- `src/web/src/features/admin/pages/Dashboard.tsx` — full rewrite of markup; hooks and data flow untouched
- `src/web/src/features/admin/pages/FeatureFlags.tsx` — full rewrite of markup; hooks and state untouched
- `src/web/src/features/admin/pages/DeadLetters.tsx` — full rewrite of markup, including topbar portal; hooks, state, and handlers untouched

**Test files modified (1):**
- `src/web/src/features/admin/__tests__/DeadLetters.test.tsx` — one assertion update for the relocated total count

**Files created:** none.
**Files deleted:** none.

---

## Task 1: Dashboard — restyle to Fieldstone

**Files:**
- Modify: `src/web/src/features/admin/pages/Dashboard.tsx` (full file rewrite of markup; delete local `StatCard` helper; rename `ChartCard` → `ChartPanel`)

**Context:** The page today renders `<h1>Analytics Dashboard</h1>` + 4-tile summary + 3 charts + a free-floating `<h2>Waitlist Stats</h2>` + 4-tile waitlist row, all inside `<div className="space-y-6 p-6">`. The local `StatCard` helper duplicates `StatTile`. Chart colors are hardcoded hex literals. The page has **no existing unit test**.

- [ ] **Step 1: Rewrite `Dashboard.tsx`**

Replace the entire file contents with:

```tsx
import type { ReactNode } from 'react';
import {
  useSummary,
  useFillRates,
  useBookingTrends,
  usePopularTimes,
  useWaitlistStats,
} from '../hooks/useAnalytics';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { StatTile } from '../components/StatTile';
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';

function ChartPanel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function EmptyChart() {
  return (
    <div className="flex h-[300px] items-center justify-center text-ink-muted text-sm">
      No data yet
    </div>
  );
}

function statValue(value: number | undefined, loading: boolean): ReactNode {
  if (loading) return <Skeleton className="h-7 w-12 inline-block" />;
  return value ?? '—';
}

export default function Dashboard() {
  const summary = useSummary();
  const fillRates = useFillRates();
  const bookingTrends = useBookingTrends();
  const popularTimes = usePopularTimes();
  const waitlistStats = useWaitlistStats();

  const fillRatesData = fillRates.data ?? [];
  const bookingTrendsData = bookingTrends.data ?? [];
  const popularTimesData = popularTimes.data ?? [];

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Analytics Dashboard</h1>}
      />

      {/* Summary tiles */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatTile
          label="Total Organizations"
          value={statValue(summary.data?.totalOrganizations, summary.isLoading)}
        />
        <StatTile
          label="Total Courses"
          value={statValue(summary.data?.totalCourses, summary.isLoading)}
        />
        <StatTile
          label="Active Users"
          value={statValue(summary.data?.activeUsers, summary.isLoading)}
        />
        <StatTile
          label="Bookings Today"
          value={statValue(summary.data?.bookingsToday, summary.isLoading)}
        />
      </div>

      {/* Fill Rates */}
      <div className="mb-6">
        <ChartPanel title="Fill Rates (Last 7 Days)">
          {fillRates.isLoading ? (
            <Skeleton className="h-[300px] w-full" />
          ) : fillRatesData.length === 0 ? (
            <EmptyChart />
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={fillRatesData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" />
                <YAxis unit="%" domain={[0, 100]} />
                <Tooltip formatter={(v) => (v != null ? `${v}%` : '—')} />
                <Line
                  type="monotone"
                  dataKey="fillPercentage"
                  name="Fill %"
                  stroke="var(--green)"
                  dot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </ChartPanel>
      </div>

      {/* Booking Trends + Popular Times */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <ChartPanel title="Booking Trends (Last 30 Days)">
          {bookingTrends.isLoading ? (
            <Skeleton className="h-[300px] w-full" />
          ) : bookingTrendsData.length === 0 ? (
            <EmptyChart />
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={bookingTrendsData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Line
                  type="monotone"
                  dataKey="bookingCount"
                  name="Bookings"
                  stroke="var(--green)"
                  dot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </ChartPanel>

        <ChartPanel title="Popular Times">
          {popularTimes.isLoading ? (
            <Skeleton className="h-[300px] w-full" />
          ) : popularTimesData.length === 0 ? (
            <EmptyChart />
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={popularTimesData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="time" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="bookingCount" name="Bookings" fill="var(--ink)" />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartPanel>
      </div>

      {/* Waitlist Stats panel */}
      <Card className="border-border-strong">
        <CardHeader>
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Waitlist Stats
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatTile
              label="Active Entries"
              value={statValue(waitlistStats.data?.activeEntries, waitlistStats.isLoading)}
            />
            <StatTile
              label="Offers Sent"
              value={statValue(waitlistStats.data?.offersSent, waitlistStats.isLoading)}
            />
            <StatTile
              label="Offers Accepted"
              value={statValue(waitlistStats.data?.offersAccepted, waitlistStats.isLoading)}
            />
            <StatTile
              label="Offers Rejected"
              value={statValue(waitlistStats.data?.offersRejected, waitlistStats.isLoading)}
            />
          </div>
        </CardContent>
      </Card>
    </>
  );
}
```

Key changes from the existing file:
- Local `StatCard` helper deleted.
- Local `ChartCard` helper renamed to `ChartPanel` and restyled with `border-border-strong` and uppercase-tracked `<CardTitle>`.
- `EmptyChart` uses `text-ink-muted` instead of `text-muted-foreground` (token-aligned; visual no-op).
- Page-local `statValue` helper consolidates the skeleton/`—`/value ternary. Not exported.
- `<PageTopbar>` contributes the title; in-body `<h1>` deleted.
- Outer `<div className="space-y-6 p-6">` deleted; returns a fragment.
- Chart strokes: Fill Rates `var(--green)`, Booking Trends `var(--green)`, Popular Times `fill="var(--ink)"`.
- Free-floating `<h2>Waitlist Stats</h2>` deleted; the four waitlist tiles are wrapped in a `<Card border-border-strong>` panel with an uppercase tracked `<CardTitle>`.
- Import of `StatTile` from `../components/StatTile` added.
- Import of `PageTopbar` from `@/components/layout/PageTopbar` added.

- [ ] **Step 2: Verify lint**

Run: `pnpm --dir src/web lint`
Expected: no errors from `Dashboard.tsx`.

- [ ] **Step 3: Verify no existing Dashboard test to regress**

Run: `ls src/web/src/features/admin/__tests__ | grep -i dashboard`
Expected: empty output (no `Dashboard.test.tsx`).

If the file does exist, stop and report — the plan needs a test-update step added.

- [ ] **Step 4: Run the admin test suite as a regression check**

Run: `pnpm --dir src/web test -- src/web/src/features/admin/__tests__/`
Expected: all tests pass (Dashboard has no test of its own; sibling tests for other admin pages are unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/admin/pages/Dashboard.tsx
git commit -m "feat(web): restyle admin Dashboard to Fieldstone

- Replace local StatCard with shared StatTile (8 call sites)
- Rename ChartCard → ChartPanel with border-border-strong + uppercase
  tracked CardTitle idiom
- Tokenize recharts colors (Fill Rates / Booking Trends → var(--green),
  Popular Times → var(--ink))
- Wrap Waitlist Stats tiles in a border-border-strong Card panel;
  delete free-floating h2
- Contribute title via PageTopbar; drop in-body h1 and outer p-6 wrapper

Part of #384.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: FeatureFlags — restyle to Fieldstone

**Files:**
- Modify: `src/web/src/features/admin/pages/FeatureFlags.tsx` (full file rewrite of markup; add `FEATURE_LABELS` map)

**Context:** The page today renders `<h1>Feature Flags</h1>` + subtitle + a plain `<Card>` wrapping an org×feature `<Table>` with kebab-case headers, all inside `<div className="p-6 space-y-6">`. State and mutation wiring are unchanged. No existing unit test.

- [ ] **Step 1: Rewrite `FeatureFlags.tsx`**

Replace the entire file contents with:

```tsx
import { useState } from 'react';
import { useOrganizations } from '../hooks/useOrganizations';
import { useSetOrgFeatures } from '../hooks/useFeatureFlags';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageTopbar } from '@/components/layout/PageTopbar';
import type { Organization } from '@/types/organization';

const FEATURE_KEYS = ['sms-notifications', 'dynamic-pricing', 'full-operator-app'] as const;
type FeatureKey = (typeof FEATURE_KEYS)[number];

const FEATURE_LABELS: Record<FeatureKey, string> = {
  'sms-notifications': 'SMS Notifications',
  'dynamic-pricing': 'Dynamic Pricing',
  'full-operator-app': 'Full Operator App',
};

type OrgFlags = Record<string, Record<FeatureKey, boolean>>;

export default function FeatureFlags() {
  const { data: organizations, isLoading, error } = useOrganizations();
  const setOrgFeatures = useSetOrgFeatures();

  const [flags, setFlags] = useState<OrgFlags>({});

  function getFlag(orgId: string, key: FeatureKey): boolean {
    return flags[orgId]?.[key] ?? false;
  }

  function handleToggle(orgId: string, key: FeatureKey, value: boolean) {
    const current = flags[orgId] ?? ({} as Record<FeatureKey, boolean>);
    const updated = { ...current, [key]: value };
    setFlags((prev) => ({ ...prev, [orgId]: updated }));
    setOrgFeatures.mutate({ orgId, flags: updated });
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Feature Flags</h1>}
      />

      <Card className="border-border-strong">
        <CardHeader>
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Organization Features
          </CardTitle>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <p className="text-ink-muted text-sm py-12 text-center">Loading organizations...</p>
          ) : error ? (
            <p className="text-destructive text-sm py-12 text-center">
              Error: {error instanceof Error ? error.message : 'Failed to load organizations'}
            </p>
          ) : !organizations || organizations.length === 0 ? (
            <p className="text-ink-muted text-sm py-12 text-center">No organizations found.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow className="bg-canvas">
                  <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">
                    Organization
                  </TableHead>
                  {FEATURE_KEYS.map((key) => (
                    <TableHead
                      key={key}
                      className="text-[10px] uppercase tracking-wider text-ink-muted whitespace-nowrap"
                    >
                      {FEATURE_LABELS[key]}
                    </TableHead>
                  ))}
                </TableRow>
              </TableHeader>
              <TableBody>
                {organizations.map((org: Organization) => (
                  <TableRow key={org.id}>
                    <TableCell className="font-medium">{org.name}</TableCell>
                    {FEATURE_KEYS.map((key) => (
                      <TableCell key={key}>
                        <Switch
                          checked={getFlag(org.id, key)}
                          onCheckedChange={(checked) => handleToggle(org.id, key, checked)}
                          aria-label={`${key} for ${org.name}`}
                        />
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </>
  );
}
```

Key changes from the existing file:
- In-body `<h1>` + subtitle deleted.
- Outer `<div className="p-6 space-y-6">` deleted; returns a fragment.
- Early-return loading and error states collapsed into the single ternary inside the Card body.
- Added `FEATURE_LABELS` map with humanized column header text (three entries).
- Card is `<Card className="border-border-strong">` with the uppercase tracked `<CardTitle>`.
- `<CardContent className="p-0">` lets the table run to the panel edges.
- `<TableHeader>` row is `bg-canvas`; each `<TableHead>` has `text-[10px] uppercase tracking-wider text-ink-muted`.
- The `<div className="overflow-x-auto">` table wrapper is deleted.
- The `aria-label` on each `<Switch>` still uses the raw kebab `${key} for ${org.name}` (test selectors preserved).
- The `FEATURE_KEYS` const, `FeatureKey` type, and `OrgFlags` state shape are unchanged.
- Mutation payload shape `{ orgId, flags: updated }` is unchanged.
- `PageTopbar` import added.

- [ ] **Step 2: Verify lint**

Run: `pnpm --dir src/web lint`
Expected: no errors from `FeatureFlags.tsx`.

- [ ] **Step 3: Verify no existing FeatureFlags test to regress**

Run: `ls src/web/src/features/admin/__tests__ | grep -i featureflags`
Expected: empty output (no `FeatureFlags.test.tsx`).

- [ ] **Step 4: Run the admin test suite as a regression check**

Run: `pnpm --dir src/web test -- src/web/src/features/admin/__tests__/`
Expected: all tests pass (`FeatureFlags` has no test of its own; sibling tests are unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/web/src/features/admin/pages/FeatureFlags.tsx
git commit -m "feat(web): restyle admin FeatureFlags to Fieldstone

- Humanize column headers via FEATURE_LABELS map (kebab keys preserved
  in data layer, mutation payload, and aria-labels)
- Wrap body in border-border-strong Card with uppercase tracked
  CardTitle; CardContent p-0 for edge-to-edge table
- Apply table restyle (bg-canvas header row, uppercase tracked heads)
- Collapse loading/error/empty early returns into in-Card ternary
- Contribute title via PageTopbar; drop in-body h1, subtitle, and
  outer p-6 wrapper

Part of #384.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: DeadLetters — restyle and portal bulk actions to topbar

**Files:**
- Modify: `src/web/src/features/admin/pages/DeadLetters.tsx` (full file rewrite of markup; portal actions; restyle `ExpandedRow`)

**Context:** The page today renders `<h1>Dead Letter Queue</h1>` + subtitle with `{page.totalCount} failed messages`, a right-side selection toolbar (`{size} selected` + Replay + Delete + AlertDialog), and a table with an expandable row pattern. There's an existing `DeadLetters.test.tsx` with 11 tests; one test asserts `screen.getByText('1 failed messages')` which breaks with the new inline count format. That test is updated in **Task 4**.

- [ ] **Step 1: Rewrite `DeadLetters.tsx`**

Replace the entire file contents with:

```tsx
import React, { useState } from 'react';
import { useDeadLetters, useReplayDeadLetters, useDeleteDeadLetters } from '../hooks/useDeadLetters';
import type { DeadLetterEnvelope } from '../hooks/useDeadLetters';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
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
import { PageTopbar } from '@/components/layout/PageTopbar';

function stripNamespace(typeName: string | undefined): string {
  if (!typeName) return 'Unknown';
  const parts = typeName.split('.');
  return parts[parts.length - 1] ?? typeName;
}

function truncate(text: string | undefined, maxLength = 80): string {
  if (!text) return '';
  if (text.length <= maxLength) return text;
  return text.slice(0, maxLength) + '\u2026';
}

function formatSentAt(isoString: string | undefined): string {
  if (!isoString) return '';
  return new Date(isoString).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

interface ExpandedRowProps {
  envelope: DeadLetterEnvelope;
}

function ExpandedRow({ envelope }: ExpandedRowProps) {
  return (
    <TableRow>
      <TableCell colSpan={5} className="bg-canvas px-6 py-4">
        <div className="space-y-3" data-testid="dead-letter-detail">
          <div>
            <p className="text-[11px] uppercase tracking-wider text-ink-muted mb-1">
              Exception Message
            </p>
            <p className="text-sm whitespace-pre-wrap break-words text-ink">
              {envelope.exceptionMessage}
            </p>
          </div>
          <div>
            <p className="text-[11px] uppercase tracking-wider text-ink-muted mb-1">
              Message Body
            </p>
            <pre className="font-mono text-[12px] bg-canvas border border-border-strong rounded-md p-3 overflow-x-auto whitespace-pre-wrap break-words">
              {JSON.stringify(envelope.message, null, 2)}
            </pre>
          </div>
        </div>
      </TableCell>
    </TableRow>
  );
}

export default function DeadLetters() {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const { data, isLoading, error } = useDeadLetters();
  const replay = useReplayDeadLetters();
  const deleteMessages = useDeleteDeadLetters();

  const page = data?.[0];
  const envelopes = page?.envelopes ?? [];

  function handleSelectAll(checked: boolean) {
    if (checked) {
      setSelectedIds(new Set(envelopes.map((e) => e.id)));
    } else {
      setSelectedIds(new Set());
    }
  }

  function handleSelectOne(id: string, checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  function handleToggleExpand(id: string) {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function handleReplay() {
    replay.mutate(Array.from(selectedIds), {
      onSuccess: () => {
        setSelectedIds(new Set());
      },
    });
  }

  function handleDelete() {
    deleteMessages.mutate(Array.from(selectedIds), {
      onSuccess: () => {
        setSelectedIds(new Set());
      },
    });
  }

  const allSelected = envelopes.length > 0 && selectedIds.size === envelopes.length;
  const someSelected = selectedIds.size > 0 && !allSelected;
  const totalCount = page?.totalCount ?? 0;

  const topbarMiddle = (
    <h1 className="font-display text-[18px] text-ink">
      Dead Letter Queue
      {totalCount > 0 && (
        <span className="ml-2 font-mono text-[13px] text-ink-muted">· {totalCount}</span>
      )}
    </h1>
  );

  const topbarRight =
    selectedIds.size > 0 ? (
      <div className="flex items-center gap-2">
        <span className="text-[12px] text-ink-muted">{selectedIds.size} selected</span>
        <Button
          variant="outline"
          size="sm"
          onClick={handleReplay}
          disabled={replay.isPending}
        >
          {replay.isPending ? 'Replaying\u2026' : 'Replay'}
        </Button>
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="destructive" size="sm" disabled={deleteMessages.isPending}>
              {deleteMessages.isPending ? 'Deleting\u2026' : 'Delete'}
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete dead letter messages?</AlertDialogTitle>
              <AlertDialogDescription>
                This will permanently delete {selectedIds.size}{' '}
                {selectedIds.size === 1 ? 'message' : 'messages'}. This action cannot be
                undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={handleDelete}>Delete</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    ) : null;

  if (isLoading) {
    return (
      <>
        <PageTopbar middle={topbarMiddle} />
        <p className="text-ink-muted text-sm py-12 text-center">
          Loading dead letter messages...
        </p>
      </>
    );
  }

  if (error) {
    return (
      <>
        <PageTopbar middle={topbarMiddle} />
        <p className="text-destructive text-sm py-12 text-center">
          Error: {error instanceof Error ? error.message : 'Failed to load dead letter messages'}
        </p>
      </>
    );
  }

  return (
    <>
      <PageTopbar middle={topbarMiddle} right={topbarRight} />

      {envelopes.length === 0 ? (
        <p className="text-ink-muted text-sm py-12 text-center">
          No dead letter messages. All clear.
        </p>
      ) : (
        <div className="border border-border-strong rounded-md bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="w-10">
                  <Checkbox
                    checked={allSelected}
                    data-state={someSelected ? 'indeterminate' : undefined}
                    onCheckedChange={(checked) => handleSelectAll(checked === true)}
                    aria-label="Select all messages"
                  />
                </TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">
                  Message Type
                </TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">
                  Exception Type
                </TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">
                  Exception Message
                </TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted whitespace-nowrap">
                  Sent At
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {envelopes.map((envelope) => (
                <React.Fragment key={envelope.id}>
                  <TableRow
                    className="cursor-pointer"
                    onClick={(e) => {
                      const target = e.target as HTMLElement;
                      if (target.closest('[role="checkbox"]')) return;
                      handleToggleExpand(envelope.id);
                    }}
                  >
                    <TableCell onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        checked={selectedIds.has(envelope.id)}
                        onCheckedChange={(checked) =>
                          handleSelectOne(envelope.id, checked === true)
                        }
                        aria-label={`Select message ${envelope.id}`}
                      />
                    </TableCell>
                    <TableCell className="font-medium font-mono text-[13px] text-ink">
                      {stripNamespace(envelope.messageType)}
                    </TableCell>
                    <TableCell className="text-[13px] text-destructive">
                      {stripNamespace(envelope.exceptionType)}
                    </TableCell>
                    <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                      {truncate(envelope.exceptionMessage)}
                    </TableCell>
                    <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted whitespace-nowrap">
                      {formatSentAt(envelope.sentAt)}
                    </TableCell>
                  </TableRow>
                  {expandedIds.has(envelope.id) && <ExpandedRow envelope={envelope} />}
                </React.Fragment>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
```

Key changes from the existing file:
- Imports: add `PageTopbar` from `@/components/layout/PageTopbar`.
- `ExpandedRow` restyled: `bg-muted/40` → `bg-canvas`; section labels to `text-[11px] uppercase tracking-wider text-ink-muted`; `<pre>` uses `font-mono text-[12px] bg-canvas border border-border-strong`; inline text gets `text-ink`.
- `data-testid="dead-letter-detail"` preserved verbatim.
- `topbarMiddle` JSX is extracted into a variable so the same node can be reused in loading/error early returns.
- `topbarRight` is `null` when no selection; otherwise the selection count + Replay + Delete + AlertDialog cluster.
- Loading and error early returns now render `<PageTopbar>` + a flat body paragraph. Outer `<div className="p-6">` wrappers deleted.
- Main return: `<PageTopbar>` + body, no outer `<div className="p-6 space-y-6">`. Returns a fragment.
- In-body header `<div className="flex items-center justify-between">` deleted entirely.
- Empty state is `<p className="text-ink-muted text-sm py-12 text-center">...</p>`; border box wrapper deleted.
- Table wrapper becomes `<div className="border border-border-strong rounded-md bg-white overflow-hidden">`.
- `<TableHeader>` row gets `bg-canvas`; each `<TableHead>` (except the checkbox column) gets `text-[10px] uppercase tracking-wider text-ink-muted`.
- Message Type / Exception Type / Exception Message / Sent At cells get tokenized `text-[13px]` or `text-[12px]` font classes.
- All state, handlers, hooks, helpers (`stripNamespace`, `truncate`, `formatSentAt`), the `AlertDialog` body, and all `aria-label`s are preserved verbatim.

- [ ] **Step 2: Verify lint**

Run: `pnpm --dir src/web lint`
Expected: no errors from `DeadLetters.tsx`.

- [ ] **Step 3: Run the DeadLetters test suite — expect one failure**

Run: `pnpm --dir src/web test -- src/web/src/features/admin/__tests__/DeadLetters.test.tsx`
Expected: 10 pass, 1 fail — the "shows total count in subtitle" test at line 180 fails because the literal text `'1 failed messages'` is gone. All other tests should pass because:
- `test-utils.tsx` provides real DOM nodes for the topbar portal slots, so `screen.getByText('BookingCreated')`, `screen.getByRole('button', { name: 'Replay' })`, `screen.getByTestId('dead-letter-detail')`, etc. all find portaled content.
- `screen.getByText('Loading dead letter messages...')` finds the new loading paragraph.
- `screen.getByText('Error: Network error')` finds the new error paragraph.
- `screen.getByText('No dead letter messages. All clear.')` finds the new empty paragraph.
- `screen.getAllByRole('checkbox')` finds the two checkboxes as before.
- Selection behavior (`fireEvent.click(messageCheckbox)` → Replay button appears) works because clicking a checkbox triggers `handleSelectOne`, which sets `selectedIds`, which flips `topbarRight` from `null` to the action cluster, which portals the buttons into the topbar-right slot.

If more than the expected one test fails, stop and investigate. The test fix lands in Task 4 along with the re-run.

- [ ] **Step 4: Commit (test still failing; fixed in Task 4)**

```bash
git add src/web/src/features/admin/pages/DeadLetters.tsx
git commit -m "feat(web): restyle admin DeadLetters to Fieldstone

- Portal bulk action cluster (count + Replay + Delete + AlertDialog)
  into PageTopbar right slot, conditional on selection
- Inline totalCount as mono suffix on topbar title (only when > 0)
- Restyle ExpandedRow (bg-canvas, tracked labels, tokenized pre block)
- Apply table restyle (bg-canvas header row, uppercase tracked heads,
  mono cells, border-border-strong wrapper)
- Flatten empty state to centered muted paragraph
- Delete in-body header row, outer p-6 wrapper, and early-return p-6
  wrappers

One existing test assertion breaks (legacy '1 failed messages' subtitle
text); fix lands in the next commit.

Part of #384.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Update the DeadLetters test for the relocated total count

**Files:**
- Modify: `src/web/src/features/admin/__tests__/DeadLetters.test.tsx:172-181`

**Context:** The last test in the file asserts `screen.getByText('1 failed messages')`. That copy no longer exists. The new location is the topbar title suffix `· 1`. The test's intent — "the total count is displayed when there's at least one message" — stays; only the matcher changes. The `test-utils.tsx` portal-slots provider makes the topbar content findable via `screen`.

- [ ] **Step 1: Update the failing assertion**

In `src/web/src/features/admin/__tests__/DeadLetters.test.tsx`, find the final test block:

```tsx
  it('shows total count in subtitle', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('1 failed messages')).toBeInTheDocument();
  });
```

Replace with:

```tsx
  it('shows total count suffix on the topbar title', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText(/·\s*1/)).toBeInTheDocument();
  });
```

Notes:
- The test name is updated because the suffix is in the topbar, not a subtitle.
- The regex matches the literal middle-dot (`·`) + optional whitespace + `1`. This asserts the count is rendered and avoids coupling to exact spacing.
- Behavior assertion (count is visible when there's at least one message) is unchanged.

- [ ] **Step 2: Run the DeadLetters test suite**

Run: `pnpm --dir src/web test -- src/web/src/features/admin/__tests__/DeadLetters.test.tsx`
Expected: all 11 tests pass.

- [ ] **Step 3: Run the full admin test suite**

Run: `pnpm --dir src/web test -- src/web/src/features/admin/__tests__/`
Expected: all tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/admin/__tests__/DeadLetters.test.tsx
git commit -m "test(web): update DeadLetters total count locator for topbar suffix

The subtitle '{totalCount} failed messages' moved into the topbar title
as a mono '· {totalCount}' suffix. Behavior assertion (count is shown
when there is data) is preserved; matcher updated to the new format.

Part of #384.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Full lint + full test + spec cross-check

**Files:** none modified.

**Context:** One last pass across the whole web workspace to catch any cross-file regressions the three page-level rewrites might have introduced.

- [ ] **Step 1: Run full frontend lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 2: Run full frontend test suite**

Run: `pnpm --dir src/web test`
Expected: all tests pass.

- [ ] **Step 3: Grep for stale references to the deleted helpers**

Run: `grep -rn "StatCard\|ChartCard" src/web/src/features/admin/`
Expected: no output. If any reference remains, it's a bug — `StatCard` was deleted in Task 1 and `ChartCard` was renamed to `ChartPanel` in the same task.

- [ ] **Step 4: Grep for stale hardcoded hex colors in Dashboard**

Run: `grep -n "#2563eb\|#16a34a\|#9333ea" src/web/src/features/admin/pages/Dashboard.tsx`
Expected: no output.

- [ ] **Step 5: Grep for stale outer wrapper classes**

Run: `grep -n "p-6 space-y-6" src/web/src/features/admin/pages/Dashboard.tsx src/web/src/features/admin/pages/FeatureFlags.tsx src/web/src/features/admin/pages/DeadLetters.tsx`
Expected: no output.

- [ ] **Step 6: Grep for stale `font-[family-name:var(--font-heading)]` inline font usage**

Run: `grep -n "font-heading" src/web/src/features/admin/pages/Dashboard.tsx src/web/src/features/admin/pages/FeatureFlags.tsx src/web/src/features/admin/pages/DeadLetters.tsx`
Expected: no output.

- [ ] **Step 7: Confirm no untracked changes**

Run: `git status`
Expected: working tree clean (all changes from Tasks 1–4 already committed).

---

## Task 6: Manual smoke test via `make dev`

**Files:** none modified.

**Context:** Per project rule, backend-adjacent work runs `make dev` to catch runtime issues that tests miss. This is a frontend-only cluster, but the project rule still applies — a dev run validates that the token cascade didn't move anything on shared surfaces and that recharts accepts the CSS variable strokes at runtime.

- [ ] **Step 1: Start the dev server**

Run: `make dev` (foreground in a terminal the user controls, or `run_in_background: true` if automation needs to poll logs).
Expected: API on :5221, web on :3000, no startup errors in the web build.

- [ ] **Step 2: Smoke `/admin/dashboard`**

Open `http://localhost:3000/admin/dashboard` in a browser logged in as platform admin. Verify:
- Topbar renders "Analytics Dashboard" in display font at ~18 px.
- Four summary tiles render with uppercase muted labels and large mono numerics.
- Fill Rates chart renders with a green line.
- Booking Trends chart renders with a green line.
- Popular Times chart renders with dark (ink-colored) bars.
- Waitlist Stats panel wraps the bottom 4 tiles inside a `border-border-strong` Card with an uppercase tracked `Waitlist Stats` header.
- No console errors related to chart color parsing.

If any chart renders a black / default-gray line or bar instead of the intended token color, recharts did not accept the CSS variable. Fix: inline the resolved hex for that one prop and document the exception in the PR description.

- [ ] **Step 3: Smoke `/admin/feature-flags`**

Verify:
- Topbar renders "Feature Flags" in display font.
- No in-body title or subtitle.
- Table is wrapped in a `border-border-strong` Card with an "Organization Features" uppercase tracked header.
- Column headers read `SMS Notifications`, `Dynamic Pricing`, `Full Operator App` (uppercase via class).
- Clicking a `<Switch>` toggles it and the mutation fires (check network tab for the `PUT` request).

- [ ] **Step 4: Smoke `/admin/dead-letters`**

Verify two scenarios:

**When there are no dead letters:**
- Topbar shows "Dead Letter Queue" with **no** `· 0` suffix.
- Body shows a centered muted paragraph "No dead letter messages. All clear."
- Topbar right is empty.

**When there are dead letters** (use the dev seed or push a message that fails handling, or mock via a dev tool if available):
- Topbar shows "Dead Letter Queue · N" in display font + mono count.
- Table renders with uppercase tracked headers.
- Clicking a row expands it to show Exception Message and Message Body (restyled expanded row).
- Clicking a message checkbox populates the topbar right with "N selected" + Replay + Delete buttons.
- Clicking Delete opens the AlertDialog; Cancel closes it without mutating; Delete triggers the mutation.
- Clicking Replay triggers the mutation and clears the selection, which empties the topbar right.

- [ ] **Step 5: Sanity-check one operator page**

Open `/operator/tee-sheet` (or whatever operator page is closest to hand) and verify it still renders correctly. Purpose: confirm that no token or class name change in Tasks 1–3 bled into the operator surface via the cascade.

- [ ] **Step 6: Sanity-check one golfer page**

Open `/golfer/walkup` (or any golfer page). Same purpose as Step 5.

- [ ] **Step 7: Stop the dev server**

Ctrl-C the `make dev` process.

---

## Task 7: Open PR

**Files:** none modified (beyond branch state).

**Context:** All tasks complete, tree clean. Open the PR so reviewers can validate.

- [ ] **Step 1: Push the branch**

Run: `git push -u origin chore/fieldstone-cluster-3-admin-system`
Expected: push succeeds.

- [ ] **Step 2: Capture before/after screenshots**

From the manual smoke session (or a fresh `make dev` run), capture one screenshot per page (three total). If you have before images from `main`, include them side-by-side.

- [ ] **Step 3: Open the PR**

Run (substituting the real screenshot paths or omitting the screenshot section if captured out-of-band and attached via the GitHub web UI):

```bash
gh pr create --title "feat(web): Fieldstone redesign — admin system (Cluster 3)" --body "$(cat <<'EOF'
## Summary

Restyles the three admin system pages — `Dashboard`, `FeatureFlags`, `DeadLetters` — to the Fieldstone design language inside the existing full-variant `<AppShell>` mount. Third of four clusters in the operator/admin redesign rollout. Falls almost entirely out of the patterns Cluster 2 established; no foundation extensions.

- **Dashboard**: local `StatCard` replaced with shared `StatTile`; `ChartCard` → `ChartPanel` with `border-border-strong` + uppercase tracked `CardTitle`; recharts colors tokenized to `var(--green)` / `var(--ink)`; Waitlist Stats tiles wrapped in a Card panel.
- **FeatureFlags**: column headers humanized via a local `FEATURE_LABELS` map (kebab keys preserved in the data layer, mutation payload, and `aria-label`s); body wrapped in a Card panel; table restyled.
- **DeadLetters**: bulk action cluster (`N selected` + Replay + Delete + AlertDialog) portaled into `<PageTopbar right>` conditionally on selection; `totalCount` rendered as an inline mono `· N` suffix on the topbar title (only when > 0); `ExpandedRow` and JSON `<pre>` restyled to Fieldstone tokens; table restyled; empty state flattened.

All three pages contribute their title via `<PageTopbar>`, render no in-body header row or subtitle, and drop their outer `p-6 space-y-6` wrapper.

## Test plan

- [x] `pnpm --dir src/web lint` clean
- [x] `pnpm --dir src/web test` clean (one locator updated in `DeadLetters.test.tsx` for the relocated total count — behavior assertion preserved)
- [x] `make dev` manual smoke green for all three pages, plus one operator + one golfer page as cascade sanity checks

## Spec & plan

- Spec: `docs/superpowers/specs/2026-04-08-admin-system-cluster-3-design.md`
- Plan: `docs/superpowers/plans/2026-04-08-admin-system-cluster-3.md`

## Screenshots

<!-- Before / after for Dashboard, FeatureFlags, DeadLetters -->

Closes #384

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Verify the PR body**

Run: `gh pr view --json title,body,baseRefName,headRefName`
Expected: title matches, `baseRefName` is `main`, `headRefName` is `chore/fieldstone-cluster-3-admin-system`, body includes the `Closes #384` keyword.

---

## Self-review

**Spec coverage check (per section of the spec):**

- **Section 1 — Architecture (none).** Confirmed: no router, navigation, or layout changes. Task 1/2/3 all assume the Cluster 2 `<AppShell>` mount.
- **Section 2 — Dashboard.** Task 1 covers topbar, summary tiles row, `ChartPanel` rename + restyle, chart palette sweep, Waitlist Stats panel, `StatCard` deletion, all "what gets removed" items.
- **Section 3 — FeatureFlags.** Task 2 covers topbar, `FEATURE_LABELS` map, Card wrap, table restyle, loading/empty/error states, all "what gets removed" items.
- **Section 4 — DeadLetters.** Task 3 covers topbar middle + right, empty state, table restyle, `ExpandedRow` restyle, loading/error flatten, all "what gets removed" items. Task 4 covers the one test assertion that the spec flags as likely-affected.
- **Section 5 — Files, tests, rollout.** Tasks 1–4 cover all three modified files + the one affected test file. Task 5 verifies no stale references. Task 6 covers manual smoke per project rule. Task 7 opens the PR with the required `Closes #384`, spec link, and screenshot callout.
- **Section 6 — Risks.** Risk 1 (recharts CSS variable strokes) is covered in Task 6 Step 2 with an explicit fallback instruction. Risks 2–6 are either no-op observations or are naturally mitigated by the TDD-light flow.
- **Done criteria checklist.** Every item traces to a step in Tasks 1–7. No gaps.

**Placeholder scan:** No TBDs, no "handle appropriate errors," no "similar to Task N," no unreferenced types. All code blocks are complete and runnable as-is.

**Type consistency:** `StatTile`'s `{ label: string; value: ReactNode }` signature is used identically in Task 1. `FeatureKey` / `FEATURE_KEYS` / `FEATURE_LABELS` / `OrgFlags` all line up in Task 2. `DeadLetterEnvelope`, `selectedIds: Set<string>`, `expandedIds: Set<string>` all match the existing hook types. `PageTopbar`'s `{ left?, middle?, right? }` props match the foundation's definition. `topbarMiddle` and `topbarRight` are `ReactNode` variables reused across three return branches in Task 3. No drift.
