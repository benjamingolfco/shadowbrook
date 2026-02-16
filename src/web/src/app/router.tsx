/* eslint-disable react-refresh/only-export-components */
import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';
import { useAuth } from '@/features/auth';
import AuthGuard from '@/features/auth/components/AuthGuard';
import RoleGuard from '@/features/auth/components/RoleGuard';
import AdminLayout from '@/components/layout/AdminLayout';
import OperatorLayout from '@/components/layout/OperatorLayout';
import GolferLayout from '@/components/layout/GolferLayout';

// Lazy-loaded pages — each feature is a separate chunk
const CourseList = lazy(() => import('@/features/admin/pages/CourseList'));
const CourseCreate = lazy(() => import('@/features/admin/pages/CourseCreate'));
const TeeSheet = lazy(() => import('@/features/operator/pages/TeeSheet'));
const TeeTimeSettings = lazy(() => import('@/features/operator/pages/TeeTimeSettings'));
const BrowseTeeTimes = lazy(() => import('@/features/golfer/pages/BrowseTeeTimes'));
const MyBookings = lazy(() => import('@/features/golfer/pages/MyBookings'));
const Profile = lazy(() => import('@/features/golfer/pages/Profile'));

function LazyPage({ children }: { children: React.ReactNode }) {
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
    path: '/admin',
    element: (
      <AuthGuard>
        <RoleGuard allowedRoles={['admin']}>
          <AdminLayout />
        </RoleGuard>
      </AuthGuard>
    ),
    children: [
      {
        path: 'courses',
        element: <LazyPage><CourseList /></LazyPage>,
      },
      {
        path: 'courses/new',
        element: <LazyPage><CourseCreate /></LazyPage>,
      },
    ],
  },
  {
    path: '/operator',
    element: (
      <AuthGuard>
        <RoleGuard allowedRoles={['operator']}>
          <OperatorLayout />
        </RoleGuard>
      </AuthGuard>
    ),
    children: [
      {
        path: 'tee-sheet',
        element: <LazyPage><TeeSheet /></LazyPage>,
      },
      {
        path: 'settings',
        element: <LazyPage><TeeTimeSettings /></LazyPage>,
      },
    ],
  },
  {
    path: '/golfer',
    element: (
      <AuthGuard>
        <RoleGuard allowedRoles={['golfer']}>
          <GolferLayout />
        </RoleGuard>
      </AuthGuard>
    ),
    children: [
      {
        path: 'tee-times',
        element: <LazyPage><BrowseTeeTimes /></LazyPage>,
      },
      {
        path: 'bookings',
        element: <LazyPage><MyBookings /></LazyPage>,
      },
      {
        path: 'profile',
        element: <LazyPage><Profile /></LazyPage>,
      },
    ],
  },
]);
