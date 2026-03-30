import { type ReactNode, useCallback, useMemo } from 'react';
import { MsalProvider, useMsal, useIsAuthenticated } from '@azure/msal-react';
import { msalInstance, loginRequest } from '@/lib/msal-config';
import { AuthContext, type AuthContextValue } from '../hooks/useAuth';
import { useMe } from '../hooks/useMe';
import type { User } from '@/types/user';

interface ProviderProps {
  children: ReactNode;
}

// ── Dev auth provider ────────────────────────────────────────────────────────
// Used when VITE_USE_DEV_AUTH=true. Always authenticated via the dev identity.

export function DevAuthProvider({ children }: ProviderProps) {
  const { data: me, isLoading } = useMe(true);

  const user: User | null = useMemo(
    () =>
      me
        ? {
            id: me.id,
            email: me.email,
            displayName: me.displayName,
            role: me.role,
            organization: me.organization,
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
    isLoading,
    permissions: user?.permissions ?? [],
    courses: user?.courses ?? [],
    login: () => {},
    logout: () => {},
    hasPermission,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ── MSAL auth content ────────────────────────────────────────────────────────
// Inner component rendered inside MsalProvider so it can call useMsal().

function MsalAuthContent({ children }: ProviderProps) {
  const { instance } = useMsal();
  const isMsalAuthenticated = useIsAuthenticated();

  const { data: me, isLoading } = useMe(isMsalAuthenticated);

  const login = useCallback(() => {
    void instance.loginRedirect(loginRequest);
  }, [instance]);

  const logout = useCallback(() => {
    void instance.logoutRedirect();
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
    isAuthenticated: isMsalAuthenticated && !!user,
    isLoading: isMsalAuthenticated ? isLoading : false,
    permissions: user?.permissions ?? [],
    courses: user?.courses ?? [],
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
