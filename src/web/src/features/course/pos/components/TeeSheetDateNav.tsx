import { Button } from '@/components/ui/button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';

export interface TeeSheetDateNavProps {
  selectedDate: string;
  onDateChange: (next: string) => void;
  courseTimeZoneId: string | undefined;
}

function addDays(dateStr: string, delta: number): string {
  const parts = dateStr.split('-').map(Number);
  const y = parts[0] ?? 0;
  const m = parts[1] ?? 1;
  const d = parts[2] ?? 1;
  const dt = new Date(Date.UTC(y, m - 1, d));
  dt.setUTCDate(dt.getUTCDate() + delta);
  return dt.toISOString().slice(0, 10);
}

export function TeeSheetDateNav({ selectedDate, onDateChange, courseTimeZoneId }: TeeSheetDateNavProps) {
  const today = getCourseToday(courseTimeZoneId ?? getBrowserTimeZone());

  return (
    <div className="flex items-center gap-1.5">
      <Button
        variant="outline"
        size="sm"
        onClick={() => onDateChange(addDays(selectedDate, -1))}
        aria-label="Previous day"
      >
        <ChevronLeft className="h-3.5 w-3.5" />
      </Button>
      <Button
        variant={selectedDate === today ? 'default' : 'outline'}
        size="sm"
        onClick={() => onDateChange(today)}
      >
        Today
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={() => onDateChange(addDays(selectedDate, 1))}
        aria-label="Next day"
      >
        <ChevronRight className="h-3.5 w-3.5" />
      </Button>
      <input
        type="date"
        value={selectedDate}
        onChange={(e) => onDateChange(e.target.value)}
        className="ml-1 h-8 rounded-[5px] border border-border bg-white px-2 text-[11px] text-ink"
        aria-label="Pick date"
      />
    </div>
  );
}
