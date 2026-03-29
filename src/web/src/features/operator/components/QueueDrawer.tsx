import { useState } from 'react';
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
    <div>
      <div className="flex items-center gap-2">
        <p className="text-sm">
          <span className={count > 0 ? 'font-bold text-lg text-foreground' : 'text-lg text-muted-foreground'}>
            {count}
          </span>{' '}
          <span className="text-muted-foreground">waiting</span>
        </p>
        {count > 0 && (
          <button
            type="button"
            className="text-xs text-muted-foreground hover:text-foreground hover:underline"
            onClick={() => setExpanded(!expanded)}
          >
            {expanded ? 'Hide queue' : 'View queue'}
          </button>
        )}
      </div>

      {expanded && (
        <div className="mt-3 border rounded-lg max-h-[320px] overflow-y-auto">
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
