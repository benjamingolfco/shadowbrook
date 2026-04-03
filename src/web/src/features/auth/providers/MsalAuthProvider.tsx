import { type ReactNode, useCallback, useMemo } from 'react';
import { MsalProvider, useMsal, useIsAuthenticated } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { msalInstance, loginRequest } from '@/lib/msal-config';
import { AuthContext, type AuthContextValue } from '../hooks/useAuth';
import { useMe } from '../hooks/useMe';
import { ApiError } from '@/lib/api-client';
import type { User } from '@/types/user';

interface ProviderProps {
  children: ReactNode;
}

// ── Dev auth provider ────────────────────────────────────────────────────────
// Used when VITE_USE_DEV_AUTH=true. Always authenticated via the dev identity.

export function DevAuthProvider({ children }: ProviderProps) {
  const { data: me, isLoading, error } = useMe(true);

  const unauthorized = error instanceof ApiError && error.status === 403;

  const user: User | null = useMemo(
    () =>
      me
        ? {
            id: me.id,
            email: me.email,
            displayName: me.displayName,
            role: me.role,
            organization: me.organization,
            organizations: me.organizations,
            courses: me.courses,
            permissions: me.permissions,
          }
        : null,
    [me],
  );

  const hasPermission = useCallback(
    (permission: string) => user?.permissions.includes(permission) ?? false,
    [user],
  );

  const value: AuthContextValue = {
    user,
    isAuthenticated: !!user,
    isLoading: unauthorized ? false : isLoading,
    unauthorized,
    permissions: user?.permissions ?? [],
    courses: user?.courses ?? [],
    organizations: user?.organizations ?? [],
    login: () => {},
    logout: () => {},
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ── MSAL auth content ────────────────────────────────────────────────────────
// Inner component rendered inside MsalProvider so it can call useMsal().

function MsalAuthContent({ children }: ProviderProps) {
  const { instance, inProgress } = useMsal();
  const isMsalAuthenticated = useIsAuthenticated();
  const isMsalBusy = inProgress !== InteractionStatus.None;

  // `useIsAuthenticated()` may lag behind the MSAL cache after redirect
  // processing. `msalInstance.getAllAccounts()` is synchronous and already
  // populated by the time React mounts (initializeMsal ran first in main.tsx).
  const hasAccounts = msalInstance.getAllAccounts().length > 0;
  const isEffectivelyAuthenticated = isMsalAuthenticated || hasAccounts;

  const { data: me, isLoading, error } = useMe(isEffectivelyAuthenticated);

  const unauthorized = error instanceof ApiError && error.status === 403;

  const login = useCallback(() => {
    void instance.loginRedirect({
      ...loginRequest,
      redirectStartPage: window.location.origin,
    });
  }, [instance]);

  const logout = useCallback(() => {
    const account = instance.getActiveAccount() ?? instance.getAllAccounts()[0];
    sessionStorage.setItem('msal_post_logout', '1');
    void instance.logoutRedirect({ account });
  }, [instance]);

  const user: User | null = useMemo(
    () =>
      me
        ? {
            id: me.id,
            email: me.email,
            displayName: me.displayName,
            role: me.role,
            organization: me.organization,
            organizations: me.organizations,
            courses: me.courses,
            permissions: me.permissions,
          }
        : null,
    [me],
  );

  const hasPermission = useCallback(
    (permission: string) => user?.permissions.includes(permission) ?? false,
    [user],
  );

  const value: AuthContextValue = {
    user,
    isAuthenticated: isEffectivelyAuthenticated && !!user,
    isLoading: unauthorized ? false : isMsalBusy || (isEffectivelyAuthenticated ? (isLoading || !user) : false),
    unauthorized,
    permissions: user?.permissions ?? [],
    courses: user?.courses ?? [],
    organizations: user?.organizations ?? [],
    login,
    logout,
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ── Public AuthProvider ──────────────────────────────────────────────────────
// Switches between dev and MSAL based on the VITE_USE_DEV_AUTH env var.

const useDevAuth = import.meta.env.VITE_USE_DEV_AUTH === 'true';

export function AuthProvider({ children }: ProviderProps) {
  if (useDevAuth) {
    return <DevAuthProvider>{children}</DevAuthProvider>;
  }

  return (
    <MsalProvider instance={msalInstance}>
      <MsalAuthContent>{children}</MsalAuthContent>
    </MsalProvider>
  );
}
