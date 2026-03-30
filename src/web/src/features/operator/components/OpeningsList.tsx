import { Badge } from '@/components/ui/badge';
import { formatWallClockTime } from '@/lib/course-time';
import type { WaitlistOpeningEntry } from '@/types/waitlist';

interface OpeningsListProps {
  openings: WaitlistOpeningEntry[];
  onCancel: (opening: WaitlistOpeningEntry) => void;
  cancellingId: string | null;
}

function getStatusVariant(status: string) {
  switch (status) {
    case 'Open':
      return 'success' as const;
    case 'Filled':
      return 'default' as const;
    case 'Expired':
      return 'secondary' as const;
    case 'Cancelled':
      return 'destructive' as const;
    default:
      return 'outline' as const;
  }
}

function formatGolferNames(golfers: WaitlistOpeningEntry['filledGolfers']): string {
  if (golfers.length === 0) return '';
  return golfers
    .map((g) => (g.groupSize > 1 ? `${g.golferName} (×${g.groupSize})` : g.golferName))
    .join(', ');
}

export function OpeningsList({ openings, onCancel, cancellingId }: OpeningsListProps) {
  if (openings.length === 0) {
    return (
      <div className="text-center py-8">
        <p className="text-sm text-muted-foreground">No openings posted yet.</p>
        <p className="text-xs text-muted-foreground mt-1">
          When you have a gap to fill, use the form above.
        </p>
      </div>
    );
  }

  const sorted = [...openings].sort((a, b) => a.teeTime.localeCompare(b.teeTime));
  const nonCancelled = sorted.filter((o) => o.status !== 'Cancelled');
  const totalFilled = nonCancelled.reduce(
    (sum, o) => sum + (o.slotsAvailable - o.slotsRemaining),
    0,
  );
  const totalSlots = nonCancelled.reduce((sum, o) => sum + o.slotsAvailable, 0);

  return (
    <div>
      <div className="flex items-baseline justify-between mb-3">
        <p className="text-sm font-medium text-muted-foreground">Today's Openings</p>
        <p className="text-xs text-muted-foreground">
          {nonCancelled.length} opening{nonCancelled.length !== 1 ? 's' : ''} · {totalFilled}/{totalSlots} filled
        </p>
      </div>

      <div className="border rounded-lg divide-y">
        {sorted.map((opening) => {
          const filled = opening.slotsAvailable - opening.slotsRemaining;
          const isCancelling = cancellingId === opening.id;
          const isFaded = opening.status === 'Expired' || opening.status === 'Cancelled';

          return (
            <div
              key={opening.id}
              className={`px-4 py-3 transition-colors duration-100 hover:bg-muted/40
                ${isFaded ? 'opacity-50' : ''}
                ${isCancelling ? 'opacity-40' : ''}
                ${opening.status === 'Filled' ? 'border-l-3 border-l-success' : ''}`}
            >
              {/*
               * Single layout that adapts via CSS grid.
               * Mobile (< md): 2 columns [auto, auto] — time+badge on row 1, details on rows 2-3
               * Desktop (md+): single flex row with fixed-width columns
               */}
              <div className="grid grid-cols-[1fr_auto] gap-x-3 gap-y-0.5 md:flex md:items-center md:gap-4">
                <span className="text-base font-semibold md:w-[80px] md:shrink-0">
                  {formatWallClockTime(opening.teeTime)}
                </span>

                <span className="md:w-[80px] md:shrink-0 justify-self-end md:justify-self-auto">
                  <Badge variant={getStatusVariant(opening.status)} className="text-xs">
                    {opening.status}
                  </Badge>
                </span>

                <span className="col-span-2 text-sm text-muted-foreground md:w-[120px] md:shrink-0">
                  {opening.status === 'Cancelled'
                    ? '—'
                    : `${filled} / ${opening.slotsAvailable} ${opening.status === 'Expired' ? 'claimed' : 'slots filled'}`}
                </span>

                <span className="col-span-2 text-sm md:flex-1 md:min-w-0 truncate">
                  {opening.filledGolfers.length > 0 ? (
                    formatGolferNames(opening.filledGolfers)
                  ) : opening.status === 'Open' ? (
                    <span className="text-muted-foreground italic">Waiting for golfers...</span>
                  ) : null}
                </span>

                <span className="col-span-2 md:w-[60px] md:shrink-0 md:text-right">
                  {opening.status === 'Open' && !isCancelling && (
                    <button
                      type="button"
                      className="text-sm text-destructive hover:underline"
                      onClick={() => onCancel(opening)}
                      aria-label={`Cancel opening at ${formatWallClockTime(opening.teeTime)}`}
                    >
                      Cancel
                    </button>
                  )}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
