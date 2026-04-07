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
      <div className="flex h-full items-center justify-center p-6" aria-label="Loading walk-up waitlist">
        <div className="w-full max-w-[860px] space-y-6">
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
          onAddGolfer={() => setAddGolferDialogOpen(true)}
          onPrintSign={() => setPrintDialogOpen(true)}
          onClose={() => setCloseDialogOpen(true)}
          onReopen={() => setReopenDialogOpen(true)}
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
            <p className="text-sm text-destructive" role="alert">Couldn't close waitlist. Try again.</p>
          )}
          {reopenMutation.isError && (
            <p className="text-sm text-destructive" role="alert">Couldn't reopen waitlist. Try again.</p>
          )}
          {removeMutation.isError && (
            <p className="text-sm text-destructive" role="alert">
              Error removing golfer: {(removeMutation.error as Error).message}
            </p>
          )}
          {cancelMutation.isError && (
            <p className="text-sm text-destructive" role="alert">
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
