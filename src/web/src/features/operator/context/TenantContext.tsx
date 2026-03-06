import { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import { setActiveTenantId } from '@/lib/api-client';

export interface SelectedTenant {
  id: string;
  organizationName: string;
}

interface TenantContextValue {
  tenant: SelectedTenant | null;
  selectTenant: (tenant: SelectedTenant) => void;
  clearTenant: () => void;
}

const TenantContext = createContext<TenantContextValue | undefined>(undefined);

const STORAGE_KEY = 'shadowbrook-dev-tenant';

interface TenantProviderProps {
  children: ReactNode;
}

export function TenantProvider({ children }: TenantProviderProps) {
  const [tenant, setTenant] = useState<SelectedTenant | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
      try {
        const parsed = JSON.parse(stored) as SelectedTenant;
        // Set immediately so the api-client module variable is ready before
        // any child component's useEffect fires and triggers data fetching.
        setActiveTenantId(parsed.id);
        return parsed;
      } catch {
        return null;
      }
    }
    return null;
  });

  useEffect(() => {
    setActiveTenantId(tenant?.id ?? null);
  }, [tenant]);

  const selectTenant = (newTenant: SelectedTenant) => {
    setTenant(newTenant);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(newTenant));
    setActiveTenantId(newTenant.id);
  };

  const clearTenant = () => {
    setTenant(null);
    localStorage.removeItem(STORAGE_KEY);
    setActiveTenantId(null);
  };

  return (
    <TenantContext.Provider value={{ tenant, selectTenant, clearTenant }}>
      {children}
    </TenantContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useTenantContext() {
  const context = useContext(TenantContext);
  if (context === undefined) {
    throw new Error('useTenantContext must be used within a TenantProvider');
  }
  return context;
}

/**
 * Safe variant that returns undefined when called outside a TenantProvider.
 * Use this when the component may render outside operator routes.
 */
// eslint-disable-next-line react-refresh/only-export-components
export function useTenantContextOptional(): TenantContextValue | undefined {
  return useContext(TenantContext);
}
