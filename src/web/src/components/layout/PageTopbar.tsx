import { type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useAppShellSlots } from './AppShellContext';

export interface PageTopbarProps {
  left?: ReactNode;
  middle?: ReactNode;
  right?: ReactNode;
}

/**
 * Pages render <PageTopbar> from inside <Outlet> to populate the AppShell topbar.
 * Each prop portals into the corresponding region.
 * Slots not provided are rendered empty (the region exists but contains nothing).
 */
export function PageTopbar({ left, middle, right }: PageTopbarProps) {
  const slots = useAppShellSlots();

  return (
    <>
      {left && slots.topbarLeft && createPortal(left, slots.topbarLeft)}
      {middle && slots.topbarMiddle && createPortal(middle, slots.topbarMiddle)}
      {right && slots.topbarRight && createPortal(right, slots.topbarRight)}
    </>
  );
}
