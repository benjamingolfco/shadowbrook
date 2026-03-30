import { type ReactNode } from 'react';
import { Navigate } from 'react-router';
import { useAuth } from '../hooks/useAuth';

interface PermissionGuardProps {
  permission: string;
  children: ReactNode;
  fallback?: string;
}

export default function PermissionGuard({ permission, children, fallback = '/' }: PermissionGuardProps) {
  const { hasPermission, isLoading } = useAuth();

  if (isLoading) return null;
  if (!hasPermission(permission)) return <Navigate to={fallback} replace />;

  return <>{children}</>;
}
