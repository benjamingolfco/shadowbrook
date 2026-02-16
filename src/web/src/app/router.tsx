/* eslint-disable react-refresh/only-export-components */
import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';
import { useAuth } from '@/features/auth';
import AuthGuard from '@/features/auth/components/AuthGuard';
import RoleGuard from '@/features/auth/components/RoleGuard';

const AdminFeature = lazy(() => import('@/features/admin'));
const OperatorFeature = lazy(() => import('@/features/operator'));
const GolferFeature = lazy(() => import('@/features/golfer'));

function LazyFeature({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<div className="p-6 text-muted-foreground">Loading...</div>}>{children}</Suspense>;
}

function RoleRedirect() {
  const { role } = useAuth();

  const routes = {
    admin: '/admin/courses',
    operator: '/operator/tee-sheet',
    golfer: '/golfer/tee-times',
  };

  return <Navigate to={routes[role]} replace />;
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <RoleRedirect />,
  },
  {
    path: '/admin/*',
    element: (
      <AuthGuard>
        <RoleGuard allowedRoles={['admin']}>
          <LazyFeature><AdminFeature /></LazyFeature>
        </RoleGuard>
      </AuthGuard>
    ),
  },
  {
    path: '/operator/*',
    element: (
      <AuthGuard>
        <RoleGuard allowedRoles={['operator']}>
          <LazyFeature><OperatorFeature /></LazyFeature>
        </RoleGuard>
      </AuthGuard>
    ),
  },
  {
    path: '/golfer/*',
    element: (
      <AuthGuard>
        <RoleGuard allowedRoles={['golfer']}>
          <LazyFeature><GolferFeature /></LazyFeature>
        </RoleGuard>
      </AuthGuard>
    ),
  },
]);
