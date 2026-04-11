import { formatWallClockTime } from '@/lib/course-time';
import { StatusBadge } from '@/components/ui/status-badge';
import { PlayerCell } from './PlayerCell';
import { mapTeeTimeStatus } from './teeSheetHelpers';

export type TeeSheetRowVariant = 'past' | 'current' | 'default';

export interface TeeSheetRowSlot {
  teeTime: string;
  status: string;
  golferName: string | null | undefined;
  playerCount: number | null | undefined;
}

export interface TeeSheetRowProps {
  slot: TeeSheetRowSlot;
  variant: TeeSheetRowVariant;
}

const VARIANT_STYLES: Record<TeeSheetRowVariant, string> = {
  past:    'bg-canvas',
  current: 'bg-card shadow-[inset_3px_0_0_var(--green)]',
  default: 'bg-paper',
};

export function TeeSheetRow({ slot, variant }: TeeSheetRowProps) {
  const isPast = variant === 'past';
  const isOpen = slot.status !== 'booked';

  return (
    <div
      className={`grid min-h-[54px] grid-cols-[100px_120px_1fr_80px] items-center gap-4 border-b border-border px-6 transition-colors hover:bg-card ${VARIANT_STYLES[variant]}`}
    >
      <div className={`font-mono text-[12px] ${isPast ? 'text-ink-muted' : 'text-ink'}`}>
        {formatWallClockTime(slot.teeTime)}
      </div>
      <div>
        <StatusBadge status={mapTeeTimeStatus(slot.status)} />
      </div>
      <PlayerCell golferName={slot.golferName} isPast={isPast} />
      <div className={`font-mono text-[12px] text-right ${isPast ? 'text-ink-muted' : 'text-ink'}`}>
        {isOpen ? '—' : (slot.playerCount ?? '—')}
      </div>
    </div>
  );
}
