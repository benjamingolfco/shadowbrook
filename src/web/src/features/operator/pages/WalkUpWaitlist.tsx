import { useState } from 'react';
import { formatCourseTime } from '@/lib/course-time';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/layout/PageHeader';
import type { PageAction } from '@/components/layout/PageHeader';
import {
  useWalkUpWaitlistToday,
  useOpenWalkUpWaitlist,
  useCloseWalkUpWaitlist,
} from '../hooks/useWalkUpWaitlist';
import { useCourseContext } from '../context/CourseContext';
import { OpenWaitlistDialog } from '../components/OpenWaitlistDialog';
import { AddGolferDialog } from '../components/AddGolferDialog';
import { AddTeeTimeRequestDialog } from '../components/AddTeeTimeRequestDialog';
import { CloseWaitlistDialog } from '../components/CloseWaitlistDialog';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import type { WalkUpWaitlistEntry, WaitlistRequestEntry } from '@/types/waitlist';

function QueueTable({ entries, timeZoneId }: { entries: WalkUpWaitlistEntry[]; timeZoneId: string }) {
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
              <TableHead>Group</TableHead>
              <TableHead>Joined At</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {entries.map((entry, index) => (
              <TableRow key={entry.id}>
                <TableCell>{index + 1}</TableCell>
                <TableCell>{entry.golferName}</TableCell>
                <TableCell>{entry.groupSize}</TableCell>
                <TableCell>{formatCourseTime(entry.joinedAt, timeZoneId)}</TableCell>
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
              {entry.groupSize > 1 && (
                <span className="text-muted-foreground text-xs">({entry.groupSize})</span>
              )}
            </div>
            <span className="text-muted-foreground">{formatCourseTime(entry.joinedAt, timeZoneId)}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

function formatTeeTime(teeTime: string): string {
  const parts = teeTime.split(':');
  const hour = parseInt(parts[0] ?? '0', 10);
  const minute = parts[1] ?? '00';
  const period = hour >= 12 ? 'PM' : 'AM';
  const hour12 = hour % 12 === 0 ? 12 : hour % 12;
  return `${hour12}:${minute} ${period}`;
}

function RequestsTable({ requests }: { requests: WaitlistRequestEntry[] }) {
  if (requests.length === 0) {
    return (
      <p className="text-muted-foreground text-sm py-4">
        No tee time requests for today.
      </p>
    );
  }

  return (
    <div>
      <p className="text-sm text-muted-foreground mb-2">
        {requests.length} request{requests.length !== 1 ? 's' : ''}
      </p>
      {/* Desktop table */}
      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Tee Time</TableHead>
              <TableHead>Golfers Needed</TableHead>
              <TableHead>Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {requests.map((request) => (
              <TableRow key={request.id}>
                <TableCell>{formatTeeTime(request.teeTime)}</TableCell>
                <TableCell>{request.golfersNeeded}</TableCell>
                <TableCell>{request.status}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {/* Mobile stacked cards */}
      <div className="md:hidden space-y-2">
        {requests.map((request) => (
          <div
            key={request.id}
            className="flex items-center justify-between rounded-md border p-3 text-sm"
          >
            <div className="flex flex-col gap-1">
              <span className="font-medium">{formatTeeTime(request.teeTime)}</span>
              <span className="text-muted-foreground text-xs">
                {request.golfersNeeded} golfer{request.golfersNeeded !== 1 ? 's' : ''} needed
              </span>
            </div>
            <span className="text-muted-foreground">{request.status}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

export default function WalkUpWaitlist() {
  const { course } = useCourseContext();
  const [copied, setCopied] = useState(false);
  const [openDialogOpen, setOpenDialogOpen] = useState(false);
  const [addGolferDialogOpen, setAddGolferDialogOpen] = useState(false);
  const [addRequestDialogOpen, setAddRequestDialogOpen] = useState(false);
  const [closeDialogOpen, setCloseDialogOpen] = useState(false);

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

  const { waitlist, entries, requests = [] } = todayQuery.data ?? { waitlist: null, entries: [], requests: [] };

  // Inactive state — no waitlist opened today
  if (!waitlist) {
    const openError = openMutation.error as (Error & { status?: number }) | null;
    const is409 = openError?.status === 409;

    const actions: PageAction[] = [
      {
        id: 'open-waitlist',
        label: 'Open Waitlist',
        description: 'Open the walk-up waitlist for today',
        onClick: () => setOpenDialogOpen(true),
        disabled: openMutation.isPending,
        disabledLabel: 'Opening...',
      },
    ];

    return (
      <div className="p-6 max-w-2xl">
        <PageHeader title="Walk-Up Waitlist" actions={actions}>
          <p className="text-muted-foreground text-sm">
            Open the waitlist to allow walk-up golfers to join the queue today.
          </p>
        </PageHeader>

        <OpenWaitlistDialog
          open={openDialogOpen}
          onOpenChange={setOpenDialogOpen}
          onConfirm={handleOpen}
        />

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

        <Tabs defaultValue="queue" className="mb-6">
          <TabsList>
            <TabsTrigger value="queue">Golfer Queue</TabsTrigger>
            <TabsTrigger value="requests">Tee Time Requests</TabsTrigger>
          </TabsList>
          <TabsContent value="queue">
            <QueueTable entries={entries} timeZoneId={course.timeZoneId} />
          </TabsContent>
          <TabsContent value="requests">
            <RequestsTable requests={requests} />
          </TabsContent>
        </Tabs>
      </div>
    );
  }

  // Active state (Open)
  const activeActions: PageAction[] = [
    {
      id: 'add-golfer',
      label: 'Add Golfer',
      description: 'Add a walk-up golfer to the waitlist',
      onClick: () => setAddGolferDialogOpen(true),
    },
    {
      id: 'add-request',
      label: 'Add Tee Time Request',
      description: 'Add a tee time request to the waitlist',
      variant: 'outline',
      onClick: () => setAddRequestDialogOpen(true),
    },
    {
      id: 'close-waitlist',
      label: 'Close Waitlist',
      description: 'Close the waitlist for today',
      variant: 'destructive',
      onClick: () => setCloseDialogOpen(true),
      disabled: closeMutation.isPending,
      disabledLabel: 'Closing...',
    },
  ];

  return (
    <div className="p-6 max-w-2xl">
      <PageHeader title="Walk-Up Waitlist" actions={activeActions}>
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

      <AddGolferDialog
        open={addGolferDialogOpen}
        onOpenChange={setAddGolferDialogOpen}
        courseId={courseId}
      />

      <AddTeeTimeRequestDialog
        open={addRequestDialogOpen}
        onOpenChange={setAddRequestDialogOpen}
        courseId={courseId}
      />

      <CloseWaitlistDialog
        open={closeDialogOpen}
        onOpenChange={setCloseDialogOpen}
        onConfirm={handleClose}
      />

      {closeMutation.isError && (
        <p className="text-destructive text-sm mb-4">
          Error closing waitlist: {(closeMutation.error as Error).message}
        </p>
      )}

      <Tabs defaultValue="queue" className="mb-6">
        <TabsList>
          <TabsTrigger value="queue">Golfer Queue</TabsTrigger>
          <TabsTrigger value="requests">Tee Time Requests</TabsTrigger>
        </TabsList>
        <TabsContent value="queue">
          <QueueTable entries={entries} timeZoneId={course.timeZoneId} />
        </TabsContent>
        <TabsContent value="requests">
          <RequestsTable requests={requests} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
