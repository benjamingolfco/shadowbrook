import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/layout/PageHeader';
import {
  useWalkUpWaitlistToday,
  useOpenWalkUpWaitlist,
  useCloseWalkUpWaitlist,
} from '../hooks/useWalkUpWaitlist';
import { useWaitlist } from '../hooks/useWaitlist';
import { useCourseContext } from '../context/CourseContext';
import { OpenWaitlistDialog } from '../components/OpenWaitlistDialog';
import { AddTeeTimeRequestDialog } from '../components/AddTeeTimeRequestDialog';
import { CloseWaitlistDialog } from '../components/CloseWaitlistDialog';
import type { WalkUpWaitlistEntry } from '@/types/waitlist';

function getTodayDate(): string {
  const today = new Date();
  const isoString = today.toISOString().split('T')[0];
  return isoString ?? '';
}

function formatJoinedAt(joinedAt: string): string {
  const date = new Date(joinedAt);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

function formatTime(timeString: string): string {
  const parts = timeString.split(':');
  const hours = parts[0] ?? '0';
  const minutes = parts[1] ?? '00';
  const hour = parseInt(hours, 10);
  const ampm = hour >= 12 ? 'PM' : 'AM';
  const displayHour = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
  return `${displayHour}:${minutes} ${ampm}`;
}

function QueueTable({ entries }: { entries: WalkUpWaitlistEntry[] }) {
  if (entries.length === 0) {
    return (
      <p className="text-muted-foreground text-sm py-4">
        No golfers have joined yet. Share the short code with walk-up golfers.
      </p>
    );
  }

  return (
    <div>
      <p className="text-sm text-muted-foreground mb-2">
        {entries.length} golfer{entries.length !== 1 ? 's' : ''} in queue
      </p>
      {/* Desktop table */}
      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead className="w-12">#</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Joined At</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.map((entry, index) => (
              <TableRow key={entry.id}>
                <TableCell>{index + 1}</TableCell>
                <TableCell>{entry.golferName}</TableCell>
                <TableCell>{formatJoinedAt(entry.joinedAt)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {/* Mobile stacked cards */}
      <div className="md:hidden space-y-2">
        {entries.map((entry, index) => (
          <div
            key={entry.id}
            className="flex items-center justify-between rounded-md border p-3 text-sm"
          >
            <div className="flex items-center gap-3">
              <span className="text-muted-foreground font-mono w-6 text-right">
                {index + 1}
              </span>
              <span className="font-medium">{entry.golferName}</span>
            </div>
            <span className="text-muted-foreground">{formatJoinedAt(entry.joinedAt)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

interface TeeTimeRequestsSectionProps {
  courseId: string;
}

function TeeTimeRequestsSection({ courseId }: TeeTimeRequestsSectionProps) {
  const todayDate = getTodayDate();
  const waitlistQuery = useWaitlist(courseId, todayDate);

  const waitlistData = waitlistQuery.data;

  if (waitlistQuery.isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-24 w-full max-w-xs" />
        <Skeleton className="h-10 w-full" />
      </div>
    );
  }

  return (
    <div>
      <h2 className="text-lg font-semibold mb-3">Tee Time Requests</h2>

      {waitlistData && (
        <Card className="w-fit min-w-48 mb-4">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total Golfers Pending
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-3xl font-bold">{waitlistData.totalGolfersPending}</p>
          </CardContent>
        </Card>
      )}

      {waitlistQuery.isError && (
        <p className="text-sm text-destructive mb-4">
          {waitlistQuery.error instanceof Error
            ? waitlistQuery.error.message
            : 'Failed to load waitlist entries'}
        </p>
      )}

      {waitlistData && waitlistData.requests.length === 0 && (
        <p className="text-muted-foreground text-sm">
          No tee time requests for today.
        </p>
      )}

      {waitlistData && waitlistData.requests.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tee Time</TableHead>
              <TableHead>Golfers Needed</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {waitlistData.requests.map((request) => (
              <TableRow key={request.id}>
                <TableCell className="font-semibold">
                  {formatTime(request.teeTime)}
                </TableCell>
                <TableCell>{request.golfersNeeded}</TableCell>
                <TableCell>
                  <Badge variant="muted">{request.golfersNeeded} pending</Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  );
}

export default function WalkUpWaitlist() {
  const { course } = useCourseContext();
  const [copied, setCopied] = useState(false);

  const todayQuery = useWalkUpWaitlistToday(course?.id);
  const openMutation = useOpenWalkUpWaitlist();
  const closeMutation = useCloseWalkUpWaitlist();

  if (!course) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <p className="text-muted-foreground">
          Select a course from the sidebar to manage the walk-up waitlist.
        </p>
      </div>
    );
  }

  const courseId = course.id;

  function handleCopyCode(shortCode: string) {
    void navigator.clipboard.writeText(shortCode).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  function handleOpen() {
    openMutation.mutate({ courseId });
  }

  function handleClose() {
    closeMutation.mutate({ courseId });
  }

  // Loading state
  if (todayQuery.isLoading) {
    return (
      <div className="p-6 max-w-2xl" aria-label="Loading walk-up waitlist">
        <PageHeader title="Walk-Up Waitlist" />
        <div className="space-y-4">
          <Skeleton className="h-8 w-48" />
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-10 w-32" />
        </div>
      </div>
    );
  }

  if (todayQuery.isError) {
    return (
      <div className="p-6 max-w-2xl">
        <PageHeader title="Walk-Up Waitlist" />
        <p className="text-destructive text-sm">
          Error loading waitlist: {todayQuery.error.message}
        </p>
      </div>
    );
  }

  const { waitlist, entries } = todayQuery.data ?? { waitlist: null, entries: [] };

  // Inactive state — no waitlist opened today
  if (!waitlist) {
    const openError = openMutation.error as (Error & { status?: number }) | null;
    const is409 = openError?.status === 409;

    return (
      <div className="p-6 max-w-2xl">
        <PageHeader
          title="Walk-Up Waitlist"
          actions={<OpenWaitlistDialog onConfirm={handleOpen} isPending={openMutation.isPending} />}
        >
          <p className="text-muted-foreground text-sm">
            Open the waitlist to allow walk-up golfers to join the queue today.
          </p>
        </PageHeader>

        {is409 && (
          <p className="text-destructive text-sm mb-4">
            Waitlist is already open for today.
          </p>
        )}

        {openMutation.isError && !is409 && (
          <p className="text-destructive text-sm mb-4">
            Error: {openError?.message}
          </p>
        )}
      </div>
    );
  }

  // Closed state
  if (waitlist.status === 'Closed') {
    return (
      <div className="p-6 max-w-2xl">
        <PageHeader title="Walk-Up Waitlist">
          <div className="flex items-center gap-2">
            <Badge variant="secondary">Closed</Badge>
          </div>
        </PageHeader>

        <div className="mb-6">
          <TeeTimeRequestsSection courseId={courseId} />
        </div>

        {entries.length > 0 && (
          <div>
            <h2 className="text-lg font-semibold mb-3">Golfer Queue</h2>
            <QueueTable entries={entries} />
          </div>
        )}

        {entries.length === 0 && (
          <p className="text-muted-foreground text-sm">No golfers joined before the waitlist closed.</p>
        )}
      </div>
    );
  }

  // Active state (Open)
  return (
    <div className="p-6 max-w-2xl">
      <PageHeader
        title="Walk-Up Waitlist"
        actions={
          <>
            <AddTeeTimeRequestDialog courseId={courseId} />
            <CloseWaitlistDialog onConfirm={handleClose} isPending={closeMutation.isPending} />
          </>
        }
      >
        <div className="flex items-center gap-3 flex-wrap">
          <Badge variant="success">Open</Badge>
          <span className="font-mono font-bold tracking-widest">
            {waitlist.shortCode.split('').join(' ')}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => handleCopyCode(waitlist.shortCode)}
          >
            {copied ? 'Copied!' : 'Copy Code'}
          </Button>
        </div>
      </PageHeader>

      {closeMutation.isError && (
        <p className="text-destructive text-sm mb-4">
          Error closing waitlist: {(closeMutation.error as Error).message}
        </p>
      )}

      <div className="mb-6">
        <TeeTimeRequestsSection courseId={courseId} />
      </div>

      <div className="mb-6">
        <h2 className="text-lg font-semibold mb-3">Golfer Queue</h2>
        <QueueTable entries={entries} />
      </div>
    </div>
  );
}
