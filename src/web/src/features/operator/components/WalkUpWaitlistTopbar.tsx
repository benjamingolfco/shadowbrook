import { useState } from 'react';
import { Copy, Check, ChevronDown, MoreHorizontal } from 'lucide-react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Button } from '@/components/ui/button';
import { StatusChip } from '@/components/ui/status-chip';
import { DrawerTrigger } from '@/components/ui/drawer';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

export interface WalkUpWaitlistTopbarProps {
  status: 'Open' | 'Closed';
  shortCode: string;
  queueCount: number;
  /** Open: Add Golfer, Print Sign, Close. Closed: Reopen. */
  onAddGolfer?: () => void;
  onPrintSign?: () => void;
  onClose?: () => void;
  onReopen?: () => void;
}

/**
 * Renders the walk-up waitlist page topbar contributions (status chip + short
 * code + queue trigger in the middle slot, overflow menu in the right slot)
 * via PageTopbar portals.
 *
 * **Precondition:** This component must be rendered as a descendant of a
 * `<Drawer>` from `@/components/ui/drawer`. The queue trigger uses
 * `<DrawerTrigger asChild>` and looks up its drawer parent via React context.
 * React context propagates through `createPortal`, so portaling the trigger
 * into the AppShell topbar slot is fine — but the React tree (not the DOM
 * tree) must include a `<Drawer>` ancestor.
 *
 * In `WalkUpWaitlist.tsx`, this is satisfied by wrapping
 * `<WalkUpWaitlistTopbar>` in `<QueueDrawer>` (which post-restyle accepts
 * children and provides the `<Drawer>` boundary).
 */
export function WalkUpWaitlistTopbar({
  status,
  shortCode,
  queueCount,
  onAddGolfer,
  onPrintSign,
  onClose,
  onReopen,
}: WalkUpWaitlistTopbarProps) {
  const [copied, setCopied] = useState(false);

  function handleCopyCode() {
    void navigator.clipboard.writeText(shortCode).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  const middle = (
    <div className="flex items-center gap-3">
      {/* Status chip */}
      <StatusChip tone={status === 'Open' ? 'green' : 'gray'}>{status}</StatusChip>

      {/* Short code + copy */}
      <div className="flex items-center gap-1">
        <span
          data-testid="short-code"
          className="font-mono text-[13px] font-semibold text-ink"
        >
          {shortCode.split('').join(' ')}
        </span>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-6 w-6 text-ink-muted hover:text-ink"
          onClick={handleCopyCode}
          aria-label="Copy short code"
        >
          {copied ? (
            <Check className="h-3.5 w-3.5 text-green" />
          ) : (
            <Copy className="h-3.5 w-3.5" />
          )}
        </Button>
      </div>

      {/* Queue trigger — wraps the QueueDrawer's trigger via DrawerTrigger asChild
          so the consumer of WalkUpWaitlistTopbar must mount this inside a <Drawer> */}
      <DrawerTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1.5 rounded-full px-1 text-[12px] hover:text-ink transition-colors"
          aria-label={`${queueCount} golfers waiting. Show queue`}
        >
          <span
            className={cn(
              'font-mono text-[13px] font-semibold',
              queueCount > 0 ? 'text-ink' : 'text-ink-muted',
            )}
          >
            {queueCount}
          </span>
          <span className="text-ink-muted">waiting</span>
          <ChevronDown className="h-3.5 w-3.5 text-ink-muted" />
        </button>
      </DrawerTrigger>
    </div>
  );

  const right = (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="h-7 w-7 text-ink-muted hover:text-ink"
          aria-label="More actions"
        >
          <MoreHorizontal className="h-4 w-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        {status === 'Open' && (
          <>
            <DropdownMenuItem onSelect={() => onAddGolfer?.()}>
              Add golfer manually
            </DropdownMenuItem>
            <DropdownMenuItem onSelect={() => onPrintSign?.()}>
              Print sign
            </DropdownMenuItem>
            <DropdownMenuItem
              onSelect={() => onClose?.()}
              className="text-destructive focus:text-destructive"
            >
              Close waitlist for today
            </DropdownMenuItem>
          </>
        )}
        {status === 'Closed' && (
          <DropdownMenuItem onSelect={() => onReopen?.()}>
            Reopen
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );

  return <PageTopbar middle={middle} right={right} />;
}
