import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

export type StatusBadgeStatus = 'booked' | 'open' | 'waitlist' | 'checkedin' | 'noshowed';

const STATUS_STYLES: Record<StatusBadgeStatus, { className: string; label: string }> = {
  booked:    { className: 'bg-green-faint text-green border-green-light',     label: 'Booked' },
  open:      { className: 'bg-canvas text-ink-muted border-border',           label: 'Open' },
  waitlist:  { className: 'bg-orange-faint text-orange border-orange-light',  label: 'Waitlist' },
  checkedin: { className: 'bg-blue-light text-blue border-blue-light',        label: 'Checked in' },
  noshowed:  { className: 'bg-red-light text-red border-red-light',           label: 'No show' },
};

export interface StatusBadgeProps {
  status: StatusBadgeStatus;
  /** Override the default label text. */
  children?: React.ReactNode;
}

export function StatusBadge({ status, children }: StatusBadgeProps) {
  const { className, label } = STATUS_STYLES[status];
  return (
    <Badge variant="outline" className={cn('rounded-[4px] px-2 py-[3px] text-[10px] font-medium border', className)}>
      {children ?? label}
    </Badge>
  );
}
