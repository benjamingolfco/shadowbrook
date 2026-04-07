import { cn } from '@/lib/utils';

export type StatusChipTone = 'green' | 'orange' | 'gray';

const TONE_STYLES: Record<StatusChipTone, { container: string; dot: string }> = {
  green:  { container: 'bg-green-faint text-green border-green-light',     dot: 'bg-green-mid' },
  orange: { container: 'bg-orange-faint text-orange border-orange-light',  dot: 'bg-orange-mid' },
  gray:   { container: 'bg-canvas text-ink-secondary border-border',       dot: 'bg-ink-faint' },
};

export interface StatusChipProps {
  tone: StatusChipTone;
  children: React.ReactNode;
}

export function StatusChip({ tone, children }: StatusChipProps) {
  const { container, dot } = TONE_STYLES[tone];
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium',
        container,
      )}
    >
      <span className={cn('h-1.5 w-1.5 shrink-0 rounded-full', dot)} />
      {children}
    </span>
  );
}
