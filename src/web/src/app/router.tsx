/* eslint-disable react-refresh/only-export-components */
import { lazy, Suspense } from 'react';
import { createBrowserRouter, Navigate, Outlet } from 'react-router';
import { useAuth, AuthProvider } from '@/features/auth';
import AuthGuard from '@/features/auth/components/AuthGuard';
import PermissionGuard from '@/features/auth/components/PermissionGuard';
import RootErrorBoundary from '@/features/error/pages/RootErrorBoundary';
import SplashScreen from '@/components/SplashScreen';

const AdminFeature = lazy(() => import('@/features/admin'));
const CourseFeature = lazy(() => import('@/features/course'));

const GolferFeature = lazy(() => import('@/features/golfer'));
const WalkupFeature = lazy(() => import('@/features/walkup'));
const WalkUpOfferFeature = lazy(() => import('@/features/walk-up'));
const WalkUpQrFeature = lazy(() => import('@/features/walkup-qr'));
const DevGolferSmsPage = lazy(() => import('@/features/dev/pages/DevGolferSmsPage'));
const StyleguidePage = import.meta.env.MODE !== 'production'
  ? lazy(() => import('@/features/dev/pages/StyleguidePage'))
  : null;

function LazyFeature({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<SplashScreen />}>{children}</Suspense>;
}

// Layout that provides AuthProvider/MsalProvider only for protected routes.
// Public routes (join, walkup, QR) render outside this layout so anonymous
// users never trigger MSAL auth redirects.
function AuthenticatedLayout() {
  return (
    <AuthProvider>
      <Outlet />
    </AuthProvider>
  );
}

function RoleRedirect() {
  const { user, isLoading, isAuthenticated } = useAuth();

  if (isLoading || !isAuthenticated || !user) return null;

  if (user.role === 'Admin') {
    return <Navigate to="/admin" replace />;
  }

  return <Navigate to="/course" replace />;
}

export const router = createBrowserRouter([
  {
    path: '/',
    ErrorBoundary: RootErrorBoundary,
    children: [
      // ── Protected routes (wrapped in AuthProvider) ──
      {
        element: <AuthenticatedLayout />,
        children: [
          {
            index: true,
            element: (
              <AuthGuard>
                <RoleRedirect />
              </AuthGuard>
            ),
          },
          {
            path: 'admin/*',
            element: (
              <AuthGuard>
                <PermissionGuard permission="users:manage" fallback="/course">
                  <LazyFeature><AdminFeature /></LazyFeature>
                </PermissionGuard>
              </AuthGuard>
            ),
          },
          {
            path: 'course/*',
            element: (
              <AuthGuard>
                <LazyFeature><CourseFeature /></LazyFeature>
              </AuthGuard>
            ),
          },
          {
            path: 'operator/*',
            element: <Navigate to="/course" replace />,
          },
        ],
      },
      // ── Public routes (no auth provider) ──
      {
        path: 'golfer/*',
        element: (
          <LazyFeature><GolferFeature /></LazyFeature>
        ),
      },
      {
        path: 'join/*',
        element: (
          <LazyFeature><WalkupFeature /></LazyFeature>
        ),
      },
      {
        path: 'book/walkup/*',
        element: (
          <LazyFeature><WalkUpOfferFeature /></LazyFeature>
        ),
      },
      {
        path: 'w/*',
        element: (
          <LazyFeature><WalkUpQrFeature /></LazyFeature>
        ),
      },
      ...((import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') ? [
        {
          path: 'dev/sms/golfer/:golferId',
          element: (
            <LazyFeature><DevGolferSmsPage /></LazyFeature>
          ),
        },
      ] : []),
      ...(StyleguidePage ? [
        {
          path: 'dev/styleguide',
          element: (
            <LazyFeature><StyleguidePage /></LazyFeature>
          ),
        },
      ] : []),
    ],
  },
]);
