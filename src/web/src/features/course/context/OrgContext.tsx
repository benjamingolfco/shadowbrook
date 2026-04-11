import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
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

const STORAGE_KEY = 'teeforce-admin-org';

interface OrgProviderProps {
  children: ReactNode;
}

export function OrgProvider({ children }: OrgProviderProps) {
  const queryClient = useQueryClient();
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
    void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
  }, [queryClient]);

  const clearOrg = useCallback(() => {
    setOrg(null);
    localStorage.removeItem(STORAGE_KEY);
    void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
  }, [queryClient]);

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
