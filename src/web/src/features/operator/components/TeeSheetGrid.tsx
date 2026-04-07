import { Fragment } from 'react';
import { TeeSheetRow, type TeeSheetRowSlot, type TeeSheetRowVariant } from './TeeSheetRow';
import { NowMarker } from './NowMarker';

export interface TeeSheetGridProps {
  slots: TeeSheetRowSlot[];
  /** Wall-clock ISO string for "now" in the course's timezone. */
  now: string;
}

function variantFor(slot: TeeSheetRowSlot, now: string, isFirstFuture: boolean): TeeSheetRowVariant {
  if (slot.teeTime < now) return 'past';
  if (isFirstFuture) return 'current';
  return 'default';
}

export function TeeSheetGrid({ slots, now }: TeeSheetGridProps) {
  // Identify the index of the first slot whose teeTime is >= now (the "current" row).
  // The NowMarker is rendered immediately before that row.
  const firstFutureIdx = slots.findIndex((s) => s.teeTime >= now);

  return (
    <div className="bg-paper">
      <div className="sticky top-0 z-10 grid grid-cols-[100px_120px_1fr_80px] gap-4 border-b border-border bg-white px-6 py-2.5">
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted">Time</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted">Status</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted">Golfer</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted text-right">Players</div>
      </div>
      {slots.map((slot, i) => (
        <Fragment key={`${slot.teeTime}-${i}`}>
          {i === firstFutureIdx && <NowMarker now={now} />}
          <TeeSheetRow slot={slot} variant={variantFor(slot, now, i === firstFutureIdx)} />
        </Fragment>
      ))}
      {firstFutureIdx === -1 && <NowMarker now={now} />}
    </div>
  );
}
