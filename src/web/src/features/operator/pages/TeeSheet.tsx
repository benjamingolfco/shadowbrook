import { useState } from 'react';
import { useTeeSheet } from '@/features/operator/hooks/useTeeSheet';
import { useCourseContext } from '../context/CourseContext';
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
  const [selectedDate, setSelectedDate] = useState<string>(getTodayDate());

  // course is guaranteed non-null by CourseGate in index.tsx
  const teeSheetQuery = useTeeSheet(course!.id, selectedDate);

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
            {teeSheetQuery.data.courseName} - {formatDate(teeSheetQuery.data.date)}
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
                      {formatTime(slot.time)}
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

function getTodayDate(): string {
  const today = new Date();
  const isoString = today.toISOString().split('T')[0];
  return isoString ?? '';
}

function formatDate(dateString: string): string {
  const parts = dateString.split('-').map(Number);
  const year = parts[0] ?? 0;
  const month = parts[1] ?? 0;
  const day = parts[2] ?? 0;
  const date = new Date(year, month - 1, day);
  return date.toLocaleDateString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
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
