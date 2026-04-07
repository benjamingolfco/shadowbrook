import { X } from 'lucide-react';
import { formatWallClockTime } from '@/lib/course-time';
import { Button } from '@/components/ui/button';
import { StatusBadge } from '@/components/ui/status-badge';
import { cn } from '@/lib/utils';
import type { WaitlistOpeningEntry } from '@/types/waitlist';
import { mapOpeningStatus, formatGolferNames } from './openingsHelpers';

const GRID_COLS = '80px 110px 80px 1fr 40px';

export interface OpeningsGridProps {
  openings: WaitlistOpeningEntry[];
  /** When true, the grid renders without cancel actions (Closed state). */
  readOnly?: boolean;
  onCancel: (opening: WaitlistOpeningEntry) => void;
  cancellingId: string | null;
}

export function OpeningsGrid({
  openings,
  readOnly = false,
  onCancel,
  cancellingId,
}: OpeningsGridProps) {
  const sorted = [...openings].sort((a, b) => a.teeTime.localeCompare(b.teeTime));
  const nonCancelled = sorted.filter((o) => o.status !== 'Cancelled');
  const totalFilled = nonCancelled.reduce(
    (sum, o) => sum + (o.slotsAvailable - o.slotsRemaining),
    0,
  );
  const totalSlots = nonCancelled.reduce((sum, o) => sum + o.slotsAvailable, 0);

  return (
    <section>
      {/* Section header line */}
      <div className="flex items-baseline justify-between border-b border-border pb-2">
        <h2 className="text-[10px] font-medium uppercase tracking-wider text-ink-muted">
          Today's Openings
        </h2>
        <p className="font-mono text-[11px] text-ink-muted">
          {nonCancelled.length} posted · {totalFilled}/{totalSlots} filled
        </p>
      </div>

      {sorted.length === 0 ? (
        <p className="px-1 py-6 text-center text-sm text-ink-muted">
          No openings posted yet.
        </p>
      ) : (
        <>
          {/* Sticky column header row */}
          <div
            className="sticky top-0 z-10 grid border-b border-border bg-white px-1 py-1.5 text-[10px] font-medium uppercase tracking-wider text-ink-muted"
            style={{ gridTemplateColumns: GRID_COLS }}
          >
            <span>Time</span>
            <span>Status</span>
            <span>Filled</span>
            <span>Golfers</span>
            <span></span>
          </div>

          {/* Rows */}
          <div className="divide-y divide-border">
            {sorted.map((opening) => {
              const filled = opening.slotsAvailable - opening.slotsRemaining;
              const isCancelling = cancellingId === opening.id;
              const isFaded = opening.status === 'Expired' || opening.status === 'Cancelled';
              const isFilled = opening.status === 'Filled';
              const showCancel = opening.status === 'Open' && !readOnly;

              return (
                <div
                  key={opening.id}
                  data-testid={`opening-row-${opening.id}`}
                  className={cn(
                    'grid items-center px-1 py-2.5 text-[13px] transition-colors',
                    isFaded ? 'bg-canvas text-ink-muted' : 'bg-paper text-ink',
                    isCancelling && 'opacity-40',
                    'hover:bg-white',
                  )}
                  style={{
                    gridTemplateColumns: GRID_COLS,
                    boxShadow: isFilled ? 'inset 3px 0 0 var(--green)' : undefined,
                  }}
                >
                  <span className={cn('font-mono text-[12px]', isFaded ? 'text-ink-muted' : 'text-ink')}>
                    {formatWallClockTime(opening.teeTime)}
                  </span>
                  <span>
                    <StatusBadge status={mapOpeningStatus(opening.status)} />
                  </span>
                  <span className="font-mono text-[12px]">
                    {opening.status === 'Cancelled' ? '—' : `${filled}/${opening.slotsAvailable}`}
                  </span>
                  <span className="min-w-0 truncate pr-2">
                    {opening.status === 'Cancelled' ? (
                      '—'
                    ) : opening.filledGolfers.length > 0 ? (
                      formatGolferNames(opening.filledGolfers)
                    ) : opening.status === 'Open' ? (
                      <span className="italic text-ink-muted">Waiting for golfers...</span>
                    ) : null}
                  </span>
                  <span className="text-right">
                    {showCancel && !isCancelling && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-7 w-7 text-ink-muted hover:text-destructive"
                        onClick={() => onCancel(opening)}
                        aria-label={`Cancel opening at ${formatWallClockTime(opening.teeTime)}`}
                      >
                        <X className="h-4 w-4" />
                      </Button>
                    )}
                  </span>
                </div>
              );
            })}
          </div>
        </>
      )}
    </section>
  );
}
