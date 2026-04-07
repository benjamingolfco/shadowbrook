import { type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useAppShellSlots } from './AppShellContext';

export interface PageRightRailProps {
  children: ReactNode;
}

/**
 * Pages render <PageRightRail>{content}</PageRightRail> to open the right rail.
 * To close it, conditionally render nothing.
 *
 * The right rail region in AppShell only mounts when at least one page is rendering this component.
 */
export function PageRightRail({ children }: PageRightRailProps) {
  const slots = useAppShellSlots();

  if (!slots.rightRail) return null;
  return createPortal(children, slots.rightRail);
}
