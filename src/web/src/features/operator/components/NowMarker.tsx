import { formatWallClockTime } from '@/lib/course-time';

export interface NowMarkerProps {
  /** Wall-clock ISO string for "now" in the course's timezone. */
  now: string;
}

export function NowMarker({ now }: NowMarkerProps) {
  return (
    <div className="pointer-events-none flex items-center gap-3 px-6 py-1">
      <div className="h-px flex-1 bg-green-mid/30" />
      <span className="font-mono text-[9px] uppercase tracking-[0.08em] text-green">
        ▶ Now · {formatWallClockTime(now)}
      </span>
      <div className="h-px flex-1 bg-green-mid/30" />
    </div>
  );
}
