import { useState } from 'react';
import { useTeeSheet } from '@/features/operator/hooks/useTeeSheet';
import { useCourseContext } from '../context/CourseContext';
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

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
      <h1 className="text-2xl font-bold">Tee Sheet</h1>
      <p className="text-muted-foreground">View the day's tee time bookings</p>

      <div className="mt-6">
        <div className="space-y-2">
          <label htmlFor="date-input" className="text-sm font-medium">
            Date
          </label>
          <input
            id="date-input"
            type="date"
            value={selectedDate}
            onChange={(e) => setSelectedDate(e.target.value)}
            className="flex h-9 w-full max-w-xs rounded-md border border-input bg-background px-3 py-1 text-base shadow-sm transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
          />
        </div>
      </div>

      {teeSheetQuery.isError && (
        <p className="mt-4 text-sm text-destructive">
          {teeSheetQuery.error instanceof Error
            ? teeSheetQuery.error.message
            : 'Failed to load tee sheet'}
        </p>
      )}

      {teeSheetQuery.data && (
        <div className="mt-6">
          <h2 className="text-xl font-semibold">
            {teeSheetQuery.data.courseName} - {teeSheetQuery.data.slots.length > 0
              ? formatDate(teeSheetQuery.data.slots[0]!.teeTime)
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
                      {formatTime(slot.teeTime)}
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

function formatDate(isoDateTime: string): string {
  const date = new Date(isoDateTime);
  return date.toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

function formatTime(isoDateTime: string): string {
  const date = new Date(isoDateTime);
  return date.toLocaleTimeString('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  });
}
