import { useState } from 'react';
import { Copy, Check } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
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
import { OpeningsList } from '../components/OpeningsList';
import { QueueDrawer } from '../components/QueueDrawer';
import { AddGolferDialog } from '../components/AddGolferDialog';
import { CloseWaitlistDialog } from '../components/CloseWaitlistDialog';
import { ReopenWaitlistDialog } from '../components/ReopenWaitlistDialog';
import { RemoveGolferDialog } from '../components/RemoveGolferDialog';
import { CancelOpeningDialog } from '../components/CancelOpeningDialog';
import { QrCodePanel } from '../components/QrCodePanel';
import type { WalkUpWaitlistEntry, WaitlistOpeningEntry } from '@/types/waitlist';

export default function WalkUpWaitlist() {
  const { course } = useCourseContext();
  const [copied, setCopied] = useState(false);
  const [addGolferDialogOpen, setAddGolferDialogOpen] = useState(false);
  const [closeDialogOpen, setCloseDialogOpen] = useState(false);
  const [reopenDialogOpen, setReopenDialogOpen] = useState(false);
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

  // Loading state
  if (todayQuery.isLoading) {
    return (
      <div className="p-6 max-w-[860px]" aria-label="Loading walk-up waitlist">
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
          Walk-Up Waitlist
        </h1>
        <div className="space-y-6 mt-6">
          <Skeleton className="h-[80px] w-full rounded-lg" />
          <Skeleton className="h-[200px] w-full rounded-lg" />
        </div>
      </div>
    );
  }

  // Error state
  if (todayQuery.isError) {
    return (
      <div className="p-6 max-w-[860px]">
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
          Walk-Up Waitlist
        </h1>
        <div className="mt-6 border rounded-lg p-6 text-center">
          <p className="font-medium">Couldn't load waitlist</p>
          <p className="text-sm text-muted-foreground mt-1">
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
        <div className="w-full max-w-md text-center space-y-4 border rounded-xl p-8">
          <h1 className="text-xl font-semibold font-[family-name:var(--font-heading)]">
            Walk-Up Waitlist
          </h1>
          <p className="text-sm text-muted-foreground">
            Open the waitlist to let walk-up golfers join the queue. You'll post tee time openings
            throughout the day.
          </p>

          <Button
            className="w-full h-11"
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

  // ── State E: Closed ──
  if (waitlist.status === 'Closed') {
    return (
      <div className="p-6 max-w-[860px]">
        <div className="flex items-start justify-between">
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
              Walk-Up Waitlist
            </h1>
            <Badge variant="secondary">Closed</Badge>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setReopenDialogOpen(true)}
            disabled={reopenMutation.isPending}
          >
            {reopenMutation.isPending ? 'Reopening...' : 'Reopen'}
          </Button>
        </div>

        {reopenMutation.isError && (
          <p className="text-sm text-destructive mt-2">
            Couldn't reopen waitlist. Try again.
          </p>
        )}

        <ReopenWaitlistDialog
          open={reopenDialogOpen}
          onOpenChange={setReopenDialogOpen}
          onConfirm={handleReopen}
        />

        <p className="text-sm text-muted-foreground mt-4">
          {entries.length} golfer{entries.length !== 1 ? 's' : ''} were on the queue
        </p>

        <div className="mt-6">
          <OpeningsList openings={openings} onCancel={() => {}} cancellingId={null} />
        </div>
      </div>
    );
  }

  // ── States B/C/D: Active ──
  return (
    <div className="p-6 max-w-[860px]">
      {/* Zone 1: Header */}
      <div className="mb-6">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
            Walk-Up Waitlist
          </h1>
          <Badge variant="success">Open</Badge>
          <span className="inline-flex items-center gap-1 whitespace-nowrap">
            <span data-testid="short-code" className="font-mono font-bold text-base md:text-lg tracking-[0.15em] md:tracking-[0.25em]">
              {waitlist.shortCode.split('').join(' ')}
            </span>
            <button
              type="button"
              className="text-muted-foreground hover:text-foreground transition-colors p-1"
              onClick={() => handleCopyCode(waitlist.shortCode)}
              aria-label="Copy short code"
            >
              {copied ? <Check className="h-4 w-4 text-success" /> : <Copy className="h-4 w-4" />}
            </button>
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 mt-2 text-sm text-muted-foreground">
          <QueueDrawer
            entries={entries}
            timeZoneId={course.timeZoneId}
            isOpen={true}
            onRemove={handleRemoveClick}
            removingEntryId={removeMutation.isPending ? removalTarget?.id ?? null : null}
          />
          <span>·</span>
          <Dialog>
            <DialogTrigger asChild>
              <button
                type="button"
                className="hover:text-foreground hover:underline"
              >
                Print sign
              </button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>QR Code Sign</DialogTitle>
              </DialogHeader>
              <QrCodePanel shortCode={waitlist.shortCode} />
            </DialogContent>
          </Dialog>
          <span>·</span>
          <button
            type="button"
            className="hover:text-foreground hover:underline"
            onClick={() => setAddGolferDialogOpen(true)}
          >
            Add golfer manually
          </button>
          <span>·</span>
          <button
            type="button"
            className="hover:text-destructive hover:underline"
            onClick={() => setCloseDialogOpen(true)}
            disabled={closeMutation.isPending}
          >
            {closeMutation.isPending ? 'Closing...' : 'Close waitlist for today'}
          </button>
        </div>
      </div>

      {/* Zone 2: Post Tee Time */}
      <div className="mb-6">
        <PostTeeTimeForm courseId={courseId} />
      </div>

      {/* Zone 3: Openings List */}
      <div className="mb-8">
        <OpeningsList
          openings={openings}
          onCancel={handleCancelClick}
          cancellingId={cancelMutation.isPending ? cancellationTarget?.id ?? null : null}
        />
      </div>

      {closeMutation.isError && (
        <p className="text-sm text-destructive mt-2">
          Couldn't close waitlist. Try again.
        </p>
      )}

      {removeMutation.isError && (
        <p className="text-sm text-destructive mt-2">
          Error removing golfer: {(removeMutation.error as Error).message}
        </p>
      )}

      {cancelMutation.isError && (
        <p className="text-sm text-destructive mt-2">
          Error cancelling opening: {(cancelMutation.error as Error).message}
        </p>
      )}

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
    </div>
  );
}
