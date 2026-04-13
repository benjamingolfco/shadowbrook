import { useState, useCallback } from 'react';
import { Link } from 'react-router';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/checkbox';
import { Skeleton } from '@/components/ui/skeleton';
import { useCourseId } from '../../hooks/useCourseId';
import { useWeeklySchedule } from '../hooks/useWeeklySchedule';
import { useBulkDraft } from '../hooks/useBulkDraft';
import { useTeeTimeSettings } from '../hooks/useTeeTimeSettings';
import { usePublishTeeSheet } from '../hooks/usePublishTeeSheet';
import { useUnpublishTeeSheet } from '../hooks/useUnpublishTeeSheet';
import type { DayStatus } from '@/types/tee-time';

const statusConfig = {
  notStarted: { label: 'Not Started', variant: 'secondary' as const },
  draft: { label: 'Draft', variant: 'outline' as const },
  published: { label: 'Published', variant: 'default' as const },
};

function getMonday(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  // day 0 = Sunday -> offset 6, day 1 = Monday -> offset 0, etc.
  const diff = day === 0 ? 6 : day - 1;
  d.setDate(d.getDate() - diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

function formatDateParam(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

function formatDayLabel(dateStr: string): string {
  // Parse YYYY-MM-DD as local date
  const parts = dateStr.split('-').map(Number);
  const date = new Date(parts[0]!, parts[1]! - 1, parts[2]!);
  const dayName = date.toLocaleDateString('en-US', { weekday: 'short' });
  const monthName = date.toLocaleDateString('en-US', { month: 'short' });
  const dayNum = date.getDate();
  return `${dayName}, ${monthName} ${dayNum}`;
}

export default function Schedule() {
  const courseId = useCourseId();
  const [weekStart, setWeekStart] = useState(() => getMonday(new Date()));
  const [selectedDates, setSelectedDates] = useState<Set<string>>(new Set());

  const startDateStr = formatDateParam(weekStart);
  const { data, isLoading, isError } = useWeeklySchedule(courseId, startDateStr);
  const bulkDraft = useBulkDraft();
  const publishTeeSheet = usePublishTeeSheet();
  const unpublishTeeSheet = useUnpublishTeeSheet();
  const { data: settings } = useTeeTimeSettings(courseId);

  const isConfigured = !!settings?.firstTeeTime;

  const navigateWeek = useCallback((direction: number) => {
    setWeekStart((prev) => {
      const next = new Date(prev);
      next.setDate(next.getDate() + direction * 7);
      return next;
    });
    setSelectedDates(new Set());
  }, []);

  const goToThisWeek = useCallback(() => {
    setWeekStart(getMonday(new Date()));
    setSelectedDates(new Set());
  }, []);

  const toggleDate = useCallback((dateStr: string) => {
    setSelectedDates((prev) => {
      const next = new Set(prev);
      if (next.has(dateStr)) {
        next.delete(dateStr);
      } else {
        next.add(dateStr);
      }
      return next;
    });
  }, []);

  const handleDraftSelected = useCallback(() => {
    const dates = Array.from(selectedDates);
    bulkDraft.mutate(
      { courseId, dates },
      { onSuccess: () => setSelectedDates(new Set()) },
    );
  }, [bulkDraft, courseId, selectedDates]);

  const weekNav = (
    <div className="flex items-center gap-2">
      <Button
        variant="ghost"
        size="icon"
        onClick={() => navigateWeek(-1)}
        aria-label="Previous week"
      >
        <ChevronLeft className="h-4 w-4" />
      </Button>
      <Button variant="outline" size="sm" onClick={goToThisWeek}>
        This Week
      </Button>
      <Button
        variant="ghost"
        size="icon"
        onClick={() => navigateWeek(1)}
        aria-label="Next week"
      >
        <ChevronRight className="h-4 w-4" />
      </Button>
    </div>
  );

  if (!isConfigured && !isLoading) {
    return (
      <>
        <PageTopbar middle={<h1 className="text-lg font-semibold">Schedule</h1>} />
        <div className="p-6">
          <p className="text-ink-muted">
            Schedule defaults are not configured.{' '}
            <Link to={`/course/${courseId}/manage/settings`} className="underline text-primary">
              Configure settings
            </Link>{' '}
            to get started.
          </p>
        </div>
      </>
    );
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="text-lg font-semibold">Schedule</h1>}
        right={weekNav}
      />
      <div className="p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium text-ink-muted">
            {formatDayLabel(startDateStr)} &ndash;{' '}
            {data?.weekEnd ? formatDayLabel(data.weekEnd) : formatDayLabel(formatDateParam(
              new Date(weekStart.getFullYear(), weekStart.getMonth(), weekStart.getDate() + 6),
            ))}
          </h2>
          <Button
            size="sm"
            disabled={selectedDates.size === 0 || bulkDraft.isPending}
            onClick={handleDraftSelected}
          >
            Draft Selected
          </Button>
        </div>

        {isLoading ? (
          <div className="grid grid-cols-7 gap-3">
            {Array.from({ length: 7 }).map((_, i) => (
              <Skeleton key={i} className="h-28 rounded-lg" />
            ))}
          </div>
        ) : isError ? (
          <div className="text-center py-12 text-ink-muted">
            <p>Unable to load schedule. Please try again later.</p>
          </div>
        ) : (
          <div className="grid grid-cols-7 gap-3">
            {data?.days.map((day: DayStatus) => (
              <DayCard
                key={day.date}
                day={day}
                courseId={courseId}
                isSelected={selectedDates.has(day.date)}
                onToggle={() => toggleDate(day.date)}
                onPublish={(date) => publishTeeSheet.mutate({ courseId, date }, {})}
                isPublishing={publishTeeSheet.isPending}
                onUnpublish={(date) => unpublishTeeSheet.mutate({ courseId, date }, {})}
                isUnpublishing={unpublishTeeSheet.isPending}
              />
            ))}
          </div>
        )}
      </div>
    </>
  );
}

interface DayCardProps {
  day: DayStatus;
  courseId: string;
  isSelected: boolean;
  onToggle: () => void;
  onPublish: (date: string) => void;
  isPublishing: boolean;
  onUnpublish: (date: string) => void;
  isUnpublishing: boolean;
}

function DayCard({ day, courseId, isSelected, onToggle, onPublish, isPublishing, onUnpublish, isUnpublishing }: DayCardProps) {
  const config = statusConfig[day.status];
  const isNotStarted = day.status === 'notStarted';
  const isDraft = day.status === 'draft';
  const isPublished = day.status === 'published';

  const cardContent = (
    <Card className={isSelected ? 'ring-2 ring-primary' : ''}>
      <CardContent className="p-4 space-y-2">
        <div className="flex items-start justify-between">
          <span className="text-sm font-medium">{formatDayLabel(day.date)}</span>
          {isNotStarted && (
            <Checkbox
              checked={isSelected}
              onCheckedChange={() => onToggle()}
              aria-label={`Select ${formatDayLabel(day.date)}`}
            />
          )}
        </div>
        <Badge variant={config.variant}>{config.label}</Badge>
        {day.intervalCount != null && (
          <p className="text-xs text-ink-muted">{day.intervalCount} intervals</p>
        )}
        {isDraft && (
          <Button
            size="sm"
            className="w-full"
            disabled={isPublishing}
            onClick={(e) => {
              e.preventDefault();
              onPublish(day.date);
            }}
          >
            Publish
          </Button>
        )}
        {isPublished && (
          <Button
            size="sm"
            variant="outline"
            className="w-full"
            disabled={isUnpublishing}
            onClick={(e) => {
              e.preventDefault();
              onUnpublish(day.date);
            }}
          >
            Unpublish
          </Button>
        )}
      </CardContent>
    </Card>
  );

  if (!isNotStarted) {
    return (
      <Link to={`/course/${courseId}/manage/schedule/${day.date}`} className="block">
        {cardContent}
      </Link>
    );
  }

  return cardContent;
}
