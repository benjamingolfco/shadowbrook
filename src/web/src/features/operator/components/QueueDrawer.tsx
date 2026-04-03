import { useState } from 'react';
import { ChevronDown } from 'lucide-react';
import { formatCourseTime } from '@/lib/course-time';
import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from '@/components/ui/drawer';
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
  const [open, setOpen] = useState(false);
  const count = entries.length;

  return (
    <Drawer open={open} onOpenChange={setOpen}>
      <DrawerTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1.5 hover:text-foreground transition-colors"
          aria-label={`${count} golfers waiting. ${open ? 'Hide' : 'Show'} queue`}
        >
          <span className={count > 0 ? 'text-xl font-semibold text-foreground' : 'text-xl font-semibold text-muted-foreground'}>
            {count}
          </span>
          <span className="text-sm text-muted-foreground">waiting</span>
          <ChevronDown className={`h-4 w-4 text-muted-foreground transition-transform duration-200 ${open ? 'rotate-180' : ''}`} />
        </button>
      </DrawerTrigger>
      <DrawerContent>
        <DrawerHeader>
          <DrawerTitle>{count} golfer{count !== 1 ? 's' : ''} waiting</DrawerTitle>
        </DrawerHeader>
        <div className="px-4 pb-4 max-h-[60vh] overflow-y-auto">
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
                        ({'\u00d7'}{entry.groupSize})
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
      </DrawerContent>
    </Drawer>
  );
}
