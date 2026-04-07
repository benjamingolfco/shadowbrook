import { createContext, useContext } from 'react';

/**
 * Portal targets that AppShell exposes to descendant pages.
 * Each ref points at a DOM node where the slot helpers will portal their content.
 * `null` means the slot is not currently mounted (e.g. minimal variant has no sidebar but always has topbar + content).
 */
export interface AppShellSlots {
  topbarLeft: HTMLDivElement | null;
  topbarMiddle: HTMLDivElement | null;
  topbarRight: HTMLDivElement | null;
  rightRail: HTMLDivElement | null;
}

const AppShellContext = createContext<AppShellSlots | null>(null);

export const AppShellProvider = AppShellContext.Provider;

// eslint-disable-next-line react-refresh/only-export-components
export function useAppShellSlots(): AppShellSlots {
  const slots = useContext(AppShellContext);
  if (!slots) {
    throw new Error(
      'AppShell slot helpers (PageTopbar, PageRightRail) must be used inside <AppShell>'
    );
  }
  return slots;
}
