import { PlayerAvatar } from './PlayerAvatar';
import { EmptySlot } from './EmptySlot';

export interface PlayerCellProps {
  golferName: string | null | undefined;
  isPast?: boolean;
}

export function PlayerCell({ golferName, isPast }: PlayerCellProps) {
  if (!golferName) {
    return (
      <div className="flex items-center">
        <EmptySlot />
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <PlayerAvatar name={golferName} tone={isPast ? 'gray' : 'green'} />
      <span className={`text-[12px] ${isPast ? 'text-ink-muted' : 'text-ink'}`}>
        {golferName}
      </span>
    </div>
  );
}
