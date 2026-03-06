import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Skeleton } from '@/components/ui/skeleton';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
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
import {
  useWalkUpWaitlistToday,
  useOpenWalkUpWaitlist,
  useCloseWalkUpWaitlist,
} from '../hooks/useWalkUpWaitlist';
import { useWaitlist, useCreateWaitlistRequest } from '../hooks/useWaitlist';
import { useCourseContext } from '../context/CourseContext';
import type { WalkUpWaitlistEntry } from '@/types/waitlist';

const addWaitlistRequestSchema = z.object({
  teeTime: z.string().min(1, 'Tee time is required'),
  golfersNeeded: z.number().min(1, 'At least 1 golfer needed').max(4, 'Maximum 4 golfers'),
});

type AddWaitlistRequestFormData = z.infer<typeof addWaitlistRequestSchema>;

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
  readOnly: boolean;
}

function TeeTimeRequestsSection({ courseId, readOnly }: TeeTimeRequestsSectionProps) {
  const todayDate = getTodayDate();
  const waitlistQuery = useWaitlist(courseId, todayDate);
  const createMutation = useCreateWaitlistRequest();

  const form = useForm<AddWaitlistRequestFormData>({
    resolver: zodResolver(addWaitlistRequestSchema),
    defaultValues: {
      teeTime: '',
      golfersNeeded: 1,
    },
  });

  function onSubmit(data: AddWaitlistRequestFormData) {
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
        },
      },
    );
  }

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

      {!readOnly && (
        <div className="mb-4">
          <h3 className="text-base font-medium mb-2">Add to Waitlist</h3>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)}>
              <div className="flex flex-col gap-4 md:flex-row md:items-end">
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
                          <SelectTrigger className="w-[120px]">
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

                <Button type="submit" disabled={createMutation.isPending}>
                  {createMutation.isPending ? 'Adding...' : 'Add to Waitlist'}
                </Button>
              </div>

              {createMutation.isSuccess && (
                <p className="mt-3 text-sm text-green-600" role="status">
                  Tee time added to waitlist.
                </p>
              )}
              {createMutation.isError && (
                <p className="mt-3 text-sm text-destructive" role="alert">
                  {createMutation.error instanceof Error
                    ? createMutation.error.message
                    : 'Failed to add tee time to waitlist.'}
                </p>
              )}
            </form>
          </Form>
        </div>
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
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Walk-Up Waitlist</h1>
        </div>
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
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Walk-Up Waitlist</h1>
        </div>
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
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Walk-Up Waitlist</h1>
          <p className="text-muted-foreground text-sm mt-1">
            Open the waitlist to allow walk-up golfers to join the queue today.
          </p>
        </div>

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

        <Button onClick={handleOpen} disabled={openMutation.isPending}>
          {openMutation.isPending ? 'Opening...' : 'Open Waitlist'}
        </Button>
      </div>
    );
  }

  // Closed state
  if (waitlist.status === 'Closed') {
    return (
      <div className="p-6 max-w-2xl">
        <div className="mb-6 flex items-center gap-3">
          <h1 className="text-2xl font-bold">Walk-Up Waitlist</h1>
          <Badge variant="secondary">Closed</Badge>
        </div>

        <div className="mb-6">
          <p className="text-sm text-muted-foreground mb-1">Short Code</p>
          <p className="text-4xl sm:text-6xl font-mono tracking-widest font-bold">
            {waitlist.shortCode.split('').join(' ')}
          </p>
        </div>

        <div className="mb-6">
          <TeeTimeRequestsSection courseId={courseId} readOnly />
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
      <div className="mb-6 flex items-center gap-3">
        <h1 className="text-2xl font-bold">Walk-Up Waitlist</h1>
        <Badge variant="success">Open</Badge>
      </div>

      <div className="mb-6">
        <p className="text-sm text-muted-foreground mb-1">Short Code</p>
        <div className="flex items-end gap-4">
          <p className="text-4xl sm:text-6xl font-mono tracking-widest font-bold">
            {waitlist.shortCode.split('').join(' ')}
          </p>
          <Button
            variant="outline"
            size="sm"
            onClick={() => handleCopyCode(waitlist.shortCode)}
            className="mb-1"
          >
            {copied ? 'Copied!' : 'Copy Code'}
          </Button>
        </div>
        <p className="text-sm text-muted-foreground mt-2">
          Share this code with walk-up golfers to let them join the queue.
        </p>
      </div>

      <div className="mb-6">
        <TeeTimeRequestsSection courseId={courseId} readOnly={false} />
      </div>

      <div className="mb-6">
        <h2 className="text-lg font-semibold mb-3">Golfer Queue</h2>
        <QueueTable entries={entries} />
      </div>

      {closeMutation.isError && (
        <p className="text-destructive text-sm mb-4">
          Error closing waitlist: {(closeMutation.error as Error).message}
        </p>
      )}

      <AlertDialog>
        <AlertDialogTrigger asChild>
          <Button variant="destructive" disabled={closeMutation.isPending}>
            {closeMutation.isPending ? 'Closing...' : 'Close Waitlist'}
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
              onClick={handleClose}
            >
              Close Waitlist
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
