import { useParams, Link } from 'react-router';
import { ChevronLeft } from 'lucide-react';
import { useCourseId } from '../../hooks/useCourseId';
import { useTeeSheet } from '../../pos/hooks/useTeeSheet';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';

export default function ScheduleDay() {
  const courseId = useCourseId();
  const { date } = useParams<{ date: string }>();
  const teeSheetQuery = useTeeSheet(courseId, date ?? '');

  if (!date) return null;

  const formattedDate = new Date(date + 'T12:00:00').toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });

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
          </div>
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
                  <span className="text-sm text-ink-muted">4 players</span>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </>
  );
}
