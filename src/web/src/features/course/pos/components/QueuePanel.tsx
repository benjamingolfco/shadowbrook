import { X } from 'lucide-react';

import { formatCourseTime } from '@/lib/course-time';
import { cn } from '@/lib/utils';
import type { WalkUpWaitlistEntry } from '@/types/waitlist';

export interface QueuePanelProps {
  entries: WalkUpWaitlistEntry[];
  timeZoneId: string;
  /** Open = waitlist active. When false (Closed state), Remove actions are hidden
   *  and Add golfer is omitted from the header. */
  isWaitlistOpen: boolean;
  onAddGolfer: () => void;
  onRemove: (entry: WalkUpWaitlistEntry) => void;
  removingEntryId: string | null;
}

/**
 * The waiting queue list. Renders inside either the AppShell right rail
 * (wide viewports) or a Sheet (narrow viewports). Layout is viewport-agnostic —
 * the container decides width and positioning.
 */
export function QueuePanel({
  entries,
  timeZoneId,
  isWaitlistOpen,
  onAddGolfer,
  onRemove,
  removingEntryId,
}: QueuePanelProps) {
  const count = entries.length;

  return (
    <div className="flex h-full flex-col">
      {/* Section header line — mirrors the Openings section header */}
      <div className="flex items-center justify-between gap-3 border-b border-border px-5 pb-3 pt-5">
        <h2 className="flex items-baseline gap-2 text-[10px] font-medium uppercase tracking-wider text-ink-muted">
          Waiting Queue
          <span
            className={cn(
              'font-mono text-[11px] tabular-nums',
              count > 0 ? 'text-ink' : 'text-ink-muted',
            )}
          >
            {count}
          </span>
        </h2>
        {isWaitlistOpen && (
          <button
            type="button"
            onClick={onAddGolfer}
            className="text-[13px] font-medium text-ink-muted transition-colors hover:text-ink"
          >
            Add golfer
          </button>
        )}
      </div>

      {/* List */}
      <div className="min-h-0 flex-1 overflow-y-auto">
        {entries.length === 0 ? (
          <p className="px-5 py-6 text-center text-sm text-ink-muted">
            {isWaitlistOpen ? 'No one waiting.' : 'Queue was empty.'}
          </p>
        ) : (
          <ol className="divide-y divide-border">
            {entries.map((entry, index) => (
              <li
                key={entry.id}
                className={cn(
                  'group flex items-center gap-3 px-5 py-2.5 text-[13px] transition-colors hover:bg-paper',
                  removingEntryId === entry.id && 'opacity-40',
                )}
              >
                <span className="w-5 shrink-0 text-right font-mono text-[11px] text-ink-muted tabular-nums">
                  {index + 1}
                </span>
                <span className="min-w-0 flex-1 truncate text-ink">
                  {entry.golferName}
                  {entry.groupSize > 1 && (
                    <span className="ml-1 text-xs text-ink-muted">×{entry.groupSize}</span>
                  )}
                </span>
                <span className="shrink-0 font-mono text-[11px] text-ink-muted tabular-nums">
                  {formatCourseTime(entry.joinedAt, timeZoneId)}
                </span>
                {isWaitlistOpen && (
                  <button
                    type="button"
                    onClick={() => onRemove(entry)}
                    disabled={removingEntryId === entry.id}
                    aria-label={`Remove ${entry.golferName} from waitlist`}
                    className="shrink-0 text-destructive transition-opacity hover:opacity-80"
                  >
                    <X className="h-4 w-4" aria-hidden="true" />
                  </button>
                )}
              </li>
            ))}
          </ol>
        )}
      </div>
    </div>
  );
}
