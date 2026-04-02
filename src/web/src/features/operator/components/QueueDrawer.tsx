import { useState } from 'react';
import { ChevronDown } from 'lucide-react';
import { formatCourseTime } from '@/lib/course-time';
import type { WalkUpWaitlistEntry } from '@/types/waitlist';

interface QueueDrawerProps {
  entries: WalkUpWaitlistEntry[];
  timeZoneId: string;
  isOpen: boolean;
  onRemove: (entry: WalkUpWaitlistEntry) => void;
  removingEntryId: string | null;
}

export function QueueDrawer({
  entries,
  timeZoneId,
  isOpen,
  onRemove,
  removingEntryId,
}: QueueDrawerProps) {
  const [expanded, setExpanded] = useState(false);
  const count = entries.length;

  return (
    <div className="relative">
      <button
        type="button"
        className="flex items-center gap-1.5 hover:text-foreground transition-colors"
        onClick={() => setExpanded(!expanded)}
        aria-expanded={expanded}
        aria-label={`${count} golfers waiting. ${expanded ? 'Hide' : 'Show'} queue`}
      >
        <span className={count > 0 ? 'text-xl font-semibold text-foreground' : 'text-xl font-semibold text-muted-foreground'}>
          {count}
        </span>
        <span className="text-sm text-muted-foreground">waiting</span>
        <ChevronDown className={`h-4 w-4 text-muted-foreground transition-transform duration-200 ${expanded ? 'rotate-180' : ''}`} />
      </button>

      {expanded && (
        <div className="absolute top-full left-0 mt-2 z-10 w-72 border rounded-lg bg-background shadow-lg max-h-[320px] overflow-y-auto">
          {entries.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-4">Queue is empty</p>
          ) : (
            <div className="divide-y">
              {entries.map((entry, index) => (
                <div
                  key={entry.id}
                  className={`flex items-center gap-3 px-3 py-2 text-sm ${
                    removingEntryId === entry.id ? 'opacity-40' : ''
                  }`}
                >
                  <span className="font-mono text-muted-foreground w-6 text-right shrink-0">
                    {index + 1}
                  </span>
                  <span className="font-medium flex-1 min-w-0">
                    {entry.golferName}
                    {entry.groupSize > 1 && (
                      <span className="text-muted-foreground text-xs ml-1">
                        (×{entry.groupSize})
                      </span>
                    )}
                  </span>
                  <span className="text-xs text-muted-foreground shrink-0">
                    {formatCourseTime(entry.joinedAt, timeZoneId)}
                  </span>
                  {isOpen && (
                    <button
                      type="button"
                      className="text-xs text-destructive hover:underline shrink-0"
                      onClick={() => onRemove(entry)}
                      disabled={removingEntryId === entry.id}
                      aria-label={`Remove ${entry.golferName} from waitlist`}
                    >
                      Remove
                    </button>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
