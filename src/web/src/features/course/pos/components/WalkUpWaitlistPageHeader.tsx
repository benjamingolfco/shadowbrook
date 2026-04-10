import { useState } from 'react';
import { Check, ChevronDown, Copy } from 'lucide-react';
import { StatusChip } from '@/components/ui/status-chip';
import { cn } from '@/lib/utils';

export interface WalkUpWaitlistPageHeaderProps {
  status: 'Open' | 'Closed';
  shortCode: string;
  queueCount: number;
  /** When true (wide viewports), the queue is visible in the right rail and
   *  the header's waiting count chip is hidden to avoid redundancy. */
  hideQueueCount?: boolean;
  onPrintSign: () => void;
  onClose: () => void;
  onReopen: () => void;
  /** Opens the queue sheet. Only invoked on narrow viewports. */
  onOpenQueue: () => void;
}

/**
 * Page-level header for the walk-up waitlist. Renders three groups
 * (status, short code, queue) with their related actions inline as
 * text buttons. The queue group is hidden at wide viewports where the
 * queue itself is visible in the right rail; at narrow widths it doubles
 * as the trigger for the queue sheet.
 */
export function WalkUpWaitlistPageHeader({
  status,
  shortCode,
  queueCount,
  hideQueueCount = false,
  onPrintSign,
  onClose,
  onReopen,
  onOpenQueue,
}: WalkUpWaitlistPageHeaderProps) {
  const [copied, setCopied] = useState(false);
  const isOpen = status === 'Open';

  function handleCopyCode() {
    void navigator.clipboard.writeText(shortCode).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  return (
    <header className="flex flex-wrap items-center justify-between gap-x-6 gap-y-4 border-b border-border pb-4">
      {/* Status group */}
      <Group>
        <StatusChip tone={isOpen ? 'green' : 'gray'}>{status}</StatusChip>
        {isOpen ? (
          <ActionLink onClick={onClose} tone="destructive">
            Close waitlist
          </ActionLink>
        ) : (
          <ActionLink onClick={onReopen}>Reopen</ActionLink>
        )}
      </Group>

      <Divider />

      {/* Short code group */}
      <Group>
        <span
          data-testid="short-code"
          className="font-mono text-base font-semibold tracking-wider text-ink"
        >
          {shortCode.split('').join(' ')}
        </span>
        <ActionLink
          onClick={handleCopyCode}
          icon={
            copied ? (
              <Check className="h-3.5 w-3.5 text-green" />
            ) : (
              <Copy className="h-3.5 w-3.5" />
            )
          }
        >
          {copied ? 'Copied' : 'Copy'}
        </ActionLink>
        <ActionLink onClick={onPrintSign}>Print sign</ActionLink>
      </Group>

      {!hideQueueCount && (
        <>
          <Divider />

          {/* Queue group — narrow viewports only; at wide widths the queue lives in the right rail */}
          <Group>
            <button
              type="button"
              onClick={onOpenQueue}
              className="flex items-baseline gap-1.5 rounded transition-colors hover:text-ink"
              aria-label={`${queueCount} golfers waiting`}
            >
              <span
                className={cn(
                  'text-xl font-semibold tabular-nums',
                  queueCount > 0 ? 'text-ink' : 'text-ink-muted',
                )}
              >
                {queueCount}
              </span>
              <span className="text-[10px] font-medium uppercase tracking-wider text-ink-muted">
                Waiting
              </span>
              <ChevronDown className="h-3.5 w-3.5 self-center text-ink-muted" />
            </button>
          </Group>
        </>
      )}
    </header>
  );
}

function Group({ children }: { children: React.ReactNode }) {
  return <div className="flex items-center gap-3">{children}</div>;
}

function Divider() {
  return <div className="hidden h-6 w-px bg-border sm:block" aria-hidden="true" />;
}

function ActionLink({
  children,
  onClick,
  icon,
  tone = 'default',
}: {
  children: React.ReactNode;
  onClick: () => void;
  icon?: React.ReactNode;
  tone?: 'default' | 'destructive';
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'inline-flex items-center gap-1 text-[13px] font-medium transition-colors',
        tone === 'destructive'
          ? 'text-ink-muted hover:text-destructive'
          : 'text-ink-muted hover:text-ink',
      )}
    >
      {icon}
      {children}
    </button>
  );
}
