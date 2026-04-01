/* eslint-disable react-refresh/only-export-components */
import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';
import { useAuth } from '@/features/auth';
import AuthGuard from '@/features/auth/components/AuthGuard';
import PermissionGuard from '@/features/auth/components/PermissionGuard';

const AdminFeature = lazy(() => import('@/features/admin'));
const OperatorFeature = lazy(() => import('@/features/operator'));
const GolferFeature = lazy(() => import('@/features/golfer'));
const WalkupFeature = lazy(() => import('@/features/walkup'));
const WalkUpOfferFeature = lazy(() => import('@/features/walk-up'));
const WalkUpQrFeature = lazy(() => import('@/features/walkup-qr'));
const DevGolferSmsPage = lazy(() => import('@/features/dev/pages/DevGolferSmsPage'));

function LazyFeature({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<div className="p-6 text-muted-foreground">Loading...</div>}>{children}</Suspense>;
}

function RoleRedirect() {
  const { user, isLoading, isAuthenticated } = useAuth();

  if (isLoading || !isAuthenticated || !user) return null;

  if (user.role === 'Admin') {
    return <Navigate to="/admin" replace />;
  }

  return <Navigate to="/operator" replace />;
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: (
      <AuthGuard>
        <RoleRedirect />
      </AuthGuard>
    ),
  },
  {
    path: '/admin/*',
    element: (
      <AuthGuard>
        <PermissionGuard permission="users:manage" fallback="/operator">
          <LazyFeature><AdminFeature /></LazyFeature>
        </PermissionGuard>
      </AuthGuard>
    ),
  },
  {
    path: '/operator/*',
    element: (
      <AuthGuard>
        <LazyFeature><OperatorFeature /></LazyFeature>
      </AuthGuard>
    ),
  },
  {
    path: '/golfer/*',
    element: (
      <LazyFeature><GolferFeature /></LazyFeature>
    ),
  },
  {
    path: '/join/*',
    element: (
      <LazyFeature><WalkupFeature /></LazyFeature>
    ),
  },
  {
    path: '/book/walkup/*',
    element: (
      <LazyFeature><WalkUpOfferFeature /></LazyFeature>
    ),
  },
  {
    path: '/w/*',
    element: (
      <LazyFeature><WalkUpQrFeature /></LazyFeature>
    ),
  },
  ...((import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') ? [{
    path: '/dev/sms/golfer/:golferId',
    element: (
      <LazyFeature><DevGolferSmsPage /></LazyFeature>
    ),
  }] : []),
]);
