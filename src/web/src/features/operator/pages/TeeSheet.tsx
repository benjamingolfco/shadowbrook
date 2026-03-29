import { useState } from 'react';
import { Link } from 'react-router';
import { useTeeSheet } from '@/features/operator/hooks/useTeeSheet';
import { useCourseContext } from '../context/CourseContext';
import { getCourseToday, getBrowserTimeZone, formatWallClockDate, formatWallClockTime } from '@/lib/course-time';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/layout/PageHeader';
import { Input } from '@/components/ui/input';

export default function TeeSheet() {
  const { course } = useCourseContext();
  const [selectedDate, setSelectedDate] = useState<string>(() =>
    getCourseToday(course?.timeZoneId ?? getBrowserTimeZone())
  );
  const teeSheetQuery = useTeeSheet(course?.id, selectedDate);

  if (!course) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <p className="text-muted-foreground">
          Select a course from the sidebar to view the tee sheet.
        </p>
      </div>
    );
  }

  return (
    <div className="p-6">
      <PageHeader title="Tee Sheet">
        <p className="text-muted-foreground">View the day's tee time bookings</p>
      </PageHeader>

      <div className="mt-6">
        <div className="space-y-2">
          <label htmlFor="date-input" className="text-sm font-medium">
            Date
          </label>
          <Input
            id="date-input"
            type="date"
            value={selectedDate}
            onChange={(e) => setSelectedDate(e.target.value)}
            className="max-w-xs"
          />
        </div>
      </div>

      {teeSheetQuery.isError && (() => {
        const message = teeSheetQuery.error instanceof Error
          ? teeSheetQuery.error.message
          : 'Failed to load tee sheet';
        const isNotConfigured = message.toLowerCase().includes('not configured');
        return isNotConfigured ? (
          <div className="mt-6 border rounded-lg p-6 text-center max-w-md">
            <p className="font-medium">Configure your tee times to get started</p>
            <p className="text-sm text-muted-foreground mt-1">
              Set your tee time interval, first tee time, and last tee time in Settings.
            </p>
            <Button asChild variant="default" size="sm" className="mt-4">
              <Link to="/operator/settings">Go to Settings</Link>
            </Button>
          </div>
        ) : (
          <p className="mt-4 text-sm text-destructive">{message}</p>
        );
      })()}

      {teeSheetQuery.data && (
        <div className="mt-6">
          <h2 className="text-xl font-semibold font-[family-name:var(--font-heading)]">
            {teeSheetQuery.data.courseName} - {teeSheetQuery.data.slots.length > 0
              ? formatWallClockDate(teeSheetQuery.data.slots[0]!.teeTime)
              : selectedDate}
          </h2>
          <div className="mt-4">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Time</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Golfer</TableHead>
                  <TableHead>Players</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {teeSheetQuery.data.slots.map((slot, index) => (
                  <TableRow key={index}>
                    <TableCell className="font-semibold">
                      {formatWallClockTime(slot.teeTime)}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant={
                          slot.status === 'booked' ? 'success' : 'muted'
                        }
                      >
                        {slot.status === 'booked' ? 'Booked' : 'Open'}
                      </Badge>
                    </TableCell>
                    <TableCell>{slot.golferName || '—'}</TableCell>
                    <TableCell>
                      {slot.status === 'booked' ? slot.playerCount : '—'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      )}
    </div>
  );
}
