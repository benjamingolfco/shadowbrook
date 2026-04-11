import { useCallback, useSyncExternalStore } from 'react';

export type ColorMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'teeforce:color-mode';
const listeners = new Set<() => void>();
let version = 0;

function getStoredMode(): ColorMode {
  if (typeof window === 'undefined') return 'system';
  const stored = window.localStorage.getItem(STORAGE_KEY);
  if (stored === 'light' || stored === 'dark' || stored === 'system') return stored;
  return 'system';
}

function systemPrefersDark(): boolean {
  if (typeof window === 'undefined') return false;
  return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

function resolve(mode: ColorMode): 'light' | 'dark' {
  return mode === 'dark' || (mode === 'system' && systemPrefersDark()) ? 'dark' : 'light';
}

function applyClass(mode: ColorMode) {
  if (typeof document === 'undefined') return;
  document.documentElement.classList.toggle('dark', resolve(mode) === 'dark');
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  const mq = typeof window !== 'undefined' ? window.matchMedia('(prefers-color-scheme: dark)') : null;
  const handler = () => {
    if (getStoredMode() === 'system') {
      applyClass('system');
      version += 1;
      listener();
    }
  };
  mq?.addEventListener('change', handler);
  return () => {
    listeners.delete(listener);
    mq?.removeEventListener('change', handler);
  };
}

function snapshot(): number {
  return version;
}

function snapshotServer(): number {
  return 0;
}

export function setColorMode(mode: ColorMode) {
  if (typeof window !== 'undefined') {
    window.localStorage.setItem(STORAGE_KEY, mode);
  }
  applyClass(mode);
  version += 1;
  for (const l of listeners) l();
}

/**
 * Apply the persisted color mode class on app startup.
 * The inline script in index.html handles the initial paint to avoid FOUC;
 * this exists as a safety net if the inline script is ever removed.
 */
export function initColorMode() {
  applyClass(getStoredMode());
}

export function useColorMode() {
  useSyncExternalStore(subscribe, snapshot, snapshotServer);
  const mode = getStoredMode();
  const resolved = resolve(mode);
  const setMode = useCallback((next: ColorMode) => setColorMode(next), []);
  return { mode, setMode, resolved };
}
