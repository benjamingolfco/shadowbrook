import { type ReactNode } from 'react';
import { Navigate } from 'react-router';
import { useAuth, type Role } from '../hooks/useAuth';

interface RoleGuardProps {
  allowedRoles: Role[];
  children: ReactNode;
}

function getHomeRouteForRole(role: Role): string {
  switch (role) {
    case 'admin':
      return '/admin/courses';
    case 'operator':
      return '/operator/tee-sheet';
    case 'golfer':
      return '/golfer/tee-times';
  }
}

export default function RoleGuard({ allowedRoles, children }: RoleGuardProps) {
  const { role } = useAuth();

  if (!allowedRoles.includes(role)) {
    return <Navigate to={getHomeRouteForRole(role)} replace />;
  }

  return <>{children}</>;
}
