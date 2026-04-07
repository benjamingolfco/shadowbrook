import { useState } from 'react';
import { formatCourseTime } from '@/lib/course-time';
import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
} from '@/components/ui/drawer';
import { cn } from '@/lib/utils';
import type { WalkUpWaitlistEntry } from '@/types/waitlist';
import type { ReactNode } from 'react';

interface QueueDrawerProps {
  entries: WalkUpWaitlistEntry[];
  timeZoneId: string;
  /** When true, Remove buttons render. False in Closed state for read-only history. */
  isOpen: boolean;
  onRemove: (entry: WalkUpWaitlistEntry) => void;
  removingEntryId: string | null;
  /** The trigger element (DrawerTrigger asChild ...). Provided by WalkUpWaitlistTopbar. */
  children?: ReactNode;
}

export function QueueDrawer({
  entries,
  timeZoneId,
  isOpen,
  onRemove,
  removingEntryId,
  children,
}: QueueDrawerProps) {
  const [open, setOpen] = useState(false);
  const count = entries.length;

  return (
    <Drawer open={open} onOpenChange={setOpen}>
      {children}
      <DrawerContent>
        <DrawerHeader>
          <DrawerTitle className="font-[family-name:var(--font-heading)] text-base">
            {count} golfer{count !== 1 ? 's' : ''} waiting
          </DrawerTitle>
        </DrawerHeader>
        <div className="max-h-[60vh] overflow-y-auto px-4 pb-4">
          {entries.length === 0 ? (
            <p className="py-4 text-center text-sm text-ink-muted">Queue is empty</p>
          ) : (
            <div>
              {entries.map((entry, index) => (
                <div
                  key={entry.id}
                  className={cn(
                    'flex items-center gap-3 border-b border-border px-3 py-2 text-sm last:border-b-0',
                    removingEntryId === entry.id && 'opacity-40',
                  )}
                >
                  <span className="w-7 shrink-0 text-right font-mono text-[12px] text-ink-muted">
                    {String(index + 1).padStart(2, '0')}
                  </span>
                  <span className="min-w-0 flex-1 text-ink">
                    {entry.golferName}
                    {entry.groupSize > 1 && (
                      <span className="ml-1 text-xs text-ink-muted">
                        (×{entry.groupSize})
                      </span>
                    )}
                  </span>
                  <span className="shrink-0 font-mono text-[11px] text-ink-muted">
                    {formatCourseTime(entry.joinedAt, timeZoneId)}
                  </span>
                  {isOpen && (
                    <button
                      type="button"
                      className="shrink-0 text-xs text-destructive hover:underline"
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
      </DrawerContent>
    </Drawer>
  );
}
