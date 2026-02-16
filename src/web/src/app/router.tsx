/* eslint-disable react-refresh/only-export-components */
import { createBrowserRouter, Navigate } from 'react-router';
import { useAuth } from '@/features/auth';
import AuthGuard from '@/features/auth/components/AuthGuard';
import RoleGuard from '@/features/auth/components/RoleGuard';
import AdminLayout from '@/components/layout/AdminLayout';
import OperatorLayout from '@/components/layout/OperatorLayout';
import GolferLayout from '@/components/layout/GolferLayout';
import CourseList from '@/features/admin/pages/CourseList';
import CourseCreate from '@/features/admin/pages/CourseCreate';
import TeeTimeSettings from '@/features/operator/pages/TeeTimeSettings';

function Placeholder({ name }: { name: string }) {
  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold">{name}</h1>
      <p className="text-muted-foreground">Coming soon</p>
    </div>
  );
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
        element: <CourseList />,
      },
      {
        path: 'courses/new',
        element: <CourseCreate />,
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
        element: <Placeholder name="Tee Sheet" />,
      },
      {
        path: 'settings',
        element: <TeeTimeSettings />,
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
        element: <Placeholder name="Browse Tee Times" />,
      },
      {
        path: 'bookings',
        element: <Placeholder name="My Bookings" />,
      },
      {
        path: 'profile',
        element: <Placeholder name="Profile" />,
      },
    ],
  },
]);
