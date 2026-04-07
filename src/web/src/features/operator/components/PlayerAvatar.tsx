import { cn } from '@/lib/utils';
import { getInitials } from './teeSheetHelpers';

export type PlayerAvatarTone = 'green' | 'orange' | 'gray';

const TONE_STYLES: Record<PlayerAvatarTone, string> = {
  green:  'bg-green-light text-green',
  orange: 'bg-orange-light text-orange',
  gray:   'bg-border-strong text-ink-muted',
};

export interface PlayerAvatarProps {
  name: string | null | undefined;
  tone?: PlayerAvatarTone;
}

export function PlayerAvatar({ name, tone = 'green' }: PlayerAvatarProps) {
  return (
    <div
      className={cn(
        'flex h-6 w-6 shrink-0 items-center justify-center rounded-[4px] text-[9px] font-semibold',
        TONE_STYLES[tone],
      )}
    >
      {getInitials(name)}
    </div>
  );
}
