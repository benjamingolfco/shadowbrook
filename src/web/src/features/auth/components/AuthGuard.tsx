import { type ReactNode } from 'react';
import { InteractionType } from '@azure/msal-browser';
import { useMsalAuthentication } from '@azure/msal-react';
import { loginRequest } from '@/lib/msal-config';
import { useAuth } from '../hooks/useAuth';

interface AuthGuardProps {
  children: ReactNode;
}

export default function AuthGuard({ children }: AuthGuardProps) {
  useMsalAuthentication(InteractionType.Redirect, loginRequest);
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading || !isAuthenticated) return null;

  return <>{children}</>;
}
