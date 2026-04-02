import { type ReactNode, useMemo } from 'react';
import { InteractionType } from '@azure/msal-browser';
import { useMsalAuthentication } from '@azure/msal-react';
import { loginRequest } from '@/lib/msal-config';
import { useAuth } from '../hooks/useAuth';

interface AuthGuardProps {
  children: ReactNode;
}

const useDevAuth = import.meta.env.VITE_USE_DEV_AUTH === 'true';

function MsalAuthGuard({ children }: AuthGuardProps) {
  const authRequest = useMemo(() => {
    const postLogout = sessionStorage.getItem('msal_post_logout');
    if (postLogout) {
      sessionStorage.removeItem('msal_post_logout');
      return { ...loginRequest, prompt: 'select_account' as const };
    }
    return loginRequest;
  }, []);
  useMsalAuthentication(InteractionType.Redirect, authRequest);
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading || !isAuthenticated) return null;

  return <>{children}</>;
}

function DevAuthGuard({ children }: AuthGuardProps) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading || !isAuthenticated) return null;

  return <>{children}</>;
}

export default function AuthGuard(props: AuthGuardProps) {
  if (useDevAuth) return <DevAuthGuard {...props} />;
  return <MsalAuthGuard {...props} />;
}
