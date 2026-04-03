import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react';
import { setAdminOrgIdGetter } from '@/lib/api-client';

export interface SelectedOrg {
  id: string;
  name: string;
}

interface OrgContextValue {
  org: SelectedOrg | null;
  selectOrg: (org: SelectedOrg) => void;
  clearOrg: () => void;
}

const OrgContext = createContext<OrgContextValue | undefined>(undefined);

const STORAGE_KEY = 'shadowbrook-admin-org';

interface OrgProviderProps {
  children: ReactNode;
}

export function OrgProvider({ children }: OrgProviderProps) {
  const [org, setOrg] = useState<SelectedOrg | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as SelectedOrg;
    } catch {
      return null;
    }
  });

  useEffect(() => {
    setAdminOrgIdGetter(() => org?.id ?? null);
    return () => setAdminOrgIdGetter(() => null);
  }, [org]);

  const selectOrg = useCallback((newOrg: SelectedOrg) => {
    setOrg(newOrg);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(newOrg));
  }, []);

  const clearOrg = useCallback(() => {
    setOrg(null);
    localStorage.removeItem(STORAGE_KEY);
  }, []);

  return (
    <OrgContext.Provider value={{ org, selectOrg, clearOrg }}>
      {children}
    </OrgContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useOrgContext() {
  const context = useContext(OrgContext);
  if (context === undefined) {
    throw new Error('useOrgContext must be used within an OrgProvider');
  }
  return context;
}
