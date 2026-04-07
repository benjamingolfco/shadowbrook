import { formatWallClockDate } from '@/lib/course-time';

export interface TeeSheetTopbarTitleProps {
  courseName: string;
  /** ISO date string (yyyy-mm-dd) of the currently selected day. */
  selectedDate: string;
  /** Optional: a tee time ISO string used to render the formatted date. Falls back to selectedDate. */
  anchorTeeTime?: string;
}

export function TeeSheetTopbarTitle({ courseName, selectedDate, anchorTeeTime }: TeeSheetTopbarTitleProps) {
  const formatted = anchorTeeTime ? formatWallClockDate(anchorTeeTime) : selectedDate;
  return (
    <div className="flex flex-col">
      <span className="text-[15px] font-semibold leading-tight text-ink">{courseName}</span>
      <span className="text-[12px] leading-tight text-ink-muted">{formatted}</span>
    </div>
  );
}
