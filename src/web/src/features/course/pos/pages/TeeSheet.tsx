import { useState } from 'react';
import { Link } from 'react-router';
import { useTeeSheet } from '../hooks/useTeeSheet';
import { useCourseId } from '../../hooks/useCourseId';
import { useCourseContext } from '../../context/CourseProvider';
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { TeeSheetTopbarTitle } from '../components/TeeSheetTopbarTitle';
import { TeeSheetDateNav } from '../components/TeeSheetDateNav';
import { TeeSheetGrid } from '../components/TeeSheetGrid';

export default function TeeSheet() {
  const courseId = useCourseId();
  const { course } = useCourseContext();
  const timeZone = course?.timeZoneId ?? getBrowserTimeZone();
  const [selectedDate, setSelectedDate] = useState<string>(() => getCourseToday(timeZone));
  const teeSheetQuery = useTeeSheet(courseId, selectedDate);

  const data = teeSheetQuery.data;
  const anchorTeeTime = data && data.slots.length > 0 ? data.slots[0]!.teeTime : undefined;
  const now = new Date().toISOString();

  return (
    <>
      <PageTopbar
        left={
          <TeeSheetTopbarTitle
            courseName={data?.courseName ?? ''}
            selectedDate={selectedDate}
            anchorTeeTime={anchorTeeTime}
          />
        }
        right={
          <TeeSheetDateNav
            selectedDate={selectedDate}
            onDateChange={setSelectedDate}
            courseTimeZoneId={timeZone}
          />
        }
      />

      {teeSheetQuery.isError && (() => {
        const message = teeSheetQuery.error instanceof Error
          ? teeSheetQuery.error.message
          : 'Failed to load tee sheet';
        const isNotConfigured = message.toLowerCase().includes('not configured');
        return isNotConfigured ? (
          <div className="m-6 max-w-md rounded-md border border-border bg-card p-6 text-center">
            <p className="font-medium text-ink">Configure your tee times to get started</p>
            <p className="mt-1 text-sm text-ink-muted">
              Set your tee time interval, first tee time, and last tee time in Settings.
            </p>
            <Button asChild variant="default" size="sm" className="mt-4">
              <Link to={`/course/${courseId}/manage/settings`}>Go to Settings</Link>
            </Button>
          </div>
        ) : (
          <p className="m-6 text-sm text-destructive">{message}</p>
        );
      })()}

      {data && <TeeSheetGrid slots={data.slots} now={now} />}
    </>
  );
}
