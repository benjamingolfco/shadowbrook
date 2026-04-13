import { useState } from 'react';
import { useParams, Link } from 'react-router';
import { ChevronLeft } from 'lucide-react';
import { useCourseId } from '../../hooks/useCourseId';
import { useTeeSheet } from '../../pos/hooks/useTeeSheet';
import { useUnpublishTeeSheet } from '../hooks/useUnpublishTeeSheet';
import { useBookingCount } from '../hooks/useBookingCount';
import { UnpublishTeeSheetDialog } from '../components/UnpublishTeeSheetDialog';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';

const statusConfig: Record<string, { label: string; variant: 'outline' | 'default' }> = {
  Draft: { label: 'Draft', variant: 'outline' },
  Published: { label: 'Published', variant: 'default' },
};

export default function ScheduleDay() {
  const courseId = useCourseId();
  const { date } = useParams<{ date: string }>() as { date: string };
  const teeSheetQuery = useTeeSheet(courseId, date ?? '');
  const unpublish = useUnpublishTeeSheet();
  const bookingCountQuery = useBookingCount(courseId, date ?? '', false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [bookingCount, setBookingCount] = useState(0);

  if (!date) return null;

  const formattedDate = new Date(date + 'T12:00:00').toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

  const status = teeSheetQuery.data?.status;
  const statusBadge = status && statusConfig[status] ? (
    <Badge variant={statusConfig[status].variant}>{statusConfig[status].label}</Badge>
  ) : null;

  async function handleUnpublish() {
    const result = await bookingCountQuery.refetch();
    const count = result.data?.count ?? 0;

    if (count === 0) {
      unpublish.mutate({ courseId, date }, {});
    } else {
      setBookingCount(count);
      setDialogOpen(true);
    }
  }

  function handleConfirmUnpublish(reason: string | undefined) {
    unpublish.mutate(
      { courseId, date, reason },
      { onSuccess: () => setDialogOpen(false) },
    );
  }

  return (
    <>
      <PageTopbar
        left={
          <div className="flex items-center gap-3">
            <Button asChild variant="ghost" size="icon">
              <Link to={`/course/${courseId}/manage/schedule`}>
                <ChevronLeft className="h-4 w-4" />
              </Link>
            </Button>
            <h1 className="font-display text-[18px] text-ink">{formattedDate}</h1>
            {statusBadge}
          </div>
        }
        right={
          status === 'Published' ? (
            <Button
              variant="outline"
              size="sm"
              onClick={handleUnpublish}
              disabled={unpublish.isPending}
            >
              Unpublish
            </Button>
          ) : undefined
        }
      />

      <div className="p-6">
        {teeSheetQuery.isLoading && (
          <div className="space-y-2">
            {Array.from({ length: 10 }).map((_, i) => (
              <Skeleton key={i} className="h-8 w-64" />
            ))}
          </div>
        )}

        {teeSheetQuery.isError && (
          <p className="text-sm text-destructive">Failed to load tee sheet intervals.</p>
        )}

        {teeSheetQuery.data && teeSheetQuery.data.slots.length === 0 && (
          <p className="text-sm text-ink-muted">No intervals found for this date.</p>
        )}

        {teeSheetQuery.data && teeSheetQuery.data.slots.length > 0 && (
          <div className="max-w-md space-y-1">
            {teeSheetQuery.data.slots.map((slot) => {
              const time = new Date(slot.teeTime).toLocaleTimeString('en-US', {
                hour: 'numeric',
                minute: '2-digit',
              });
              return (
                <div
                  key={slot.teeTime}
                  className="flex items-center justify-between rounded-md border border-border px-4 py-2"
                >
                  <span className="text-sm font-medium text-ink">{time}</span>
                  <span className="text-sm text-ink-muted">{slot.playerCount} players</span>
                </div>
              );
            })}
          </div>
        )}
      </div>

      <UnpublishTeeSheetDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        onConfirm={handleConfirmUnpublish}
        isPending={unpublish.isPending}
        bookingCount={bookingCount}
      />
    </>
  );
}
