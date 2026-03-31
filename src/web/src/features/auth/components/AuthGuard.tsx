import { type ReactNode, useEffect, useRef } from 'react';
import { useMsal } from '@azure/msal-react';
import { InteractionStatus } from '@azure/msal-browser';
import { useAuth } from '../hooks/useAuth';

interface AuthGuardProps {
  children: ReactNode;
}

export default function AuthGuard({ children }: AuthGuardProps) {
  const { isAuthenticated, isLoading, login } = useAuth();
  const { inProgress } = useMsal();
  const loginTriggered = useRef(false);

  useEffect(() => {
    // Don't trigger login if a redirect response is pending in the URL hash
    const hasRedirectResponse = window.location.hash.includes('code=');
    if (hasRedirectResponse) return;

    if (!isLoading && !isAuthenticated && inProgress === InteractionStatus.None && !loginTriggered.current) {
      loginTriggered.current = true;
      login();
    }
  }, [isLoading, isAuthenticated, inProgress, login]);

  // Reset the ref when authentication succeeds so logout can re-trigger login
  useEffect(() => {
    if (isAuthenticated) {
      loginTriggered.current = false;
    }
  }, [isAuthenticated]);

  if (isLoading || !isAuthenticated) return null;

  return <>{children}</>;
}
