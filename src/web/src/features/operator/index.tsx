import { useEffect, type ReactNode } from 'react';
import { Routes, Route, Navigate, Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { useOperatorShellProps } from './hooks/useOperatorShellProps';
import TeeSheet from './pages/TeeSheet';
import TeeTimeSettings from './pages/TeeTimeSettings';
import WalkUpWaitlist from './pages/WalkUpWaitlist';
import CoursePortfolio from './pages/CoursePortfolio';
import OrgPicker from './pages/OrgPicker';
import { CourseProvider, useCourseContext } from './context/CourseContext';
import { OrgProvider, useOrgContext } from './context/OrgContext';
import { ThemeProvider } from '@/components/ThemeProvider';
import { useFeature } from '@/hooks/use-features';
import { useAuth } from '@/features/auth';

/**
 * Local wrapper component that lets us call the `useOperatorShellProps` hook
 * from inside React Router's `element` prop. Each operator route branch picks
 * a variant ('full' or 'minimal'); the hook supplies the brand, nav config,
 * and switch-course handler.
 *
 * This is the minimum glue needed to satisfy React Router's element-prop
 * contract — it carries no logic of its own and replaces the deleted
 * `OperatorLayout.tsx` and `WaitlistShellLayout.tsx` shims.
 */
function OperatorShell({ variant, children }: { variant: 'full' | 'minimal'; children: ReactNode }) {
  const shellProps = useOperatorShellProps(variant);
  return <AppShell {...shellProps}>{children}</AppShell>;
}

function OrgGate() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  if (isAdmin) {
    return <AdminOrgGate />;
  }

  return <CourseGate />;
}

function AdminOrgGate() {
  const { org } = useOrgContext();

  if (!org) {
    return (
      <Routes>
        <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
          <Route path="*" element={<OrgPicker />} />
        </Route>
      </Routes>
    );
  }

  return <CourseGate />;
}

function CourseGate() {
  const { course, clearCourse } = useCourseContext();
  const { courses } = useAuth();
  const fullOperatorApp = useFeature('full-operator-app', course?.id);

  useEffect(() => {
    if (course && !courses.some((c) => c.id === course.id)) {
      clearCourse();
    }
  }, [course, courses, clearCourse]);

  if (!course) {
    if (fullOperatorApp) {
      return (
        <Routes>
          <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
            <Route path="*" element={<CoursePortfolio />} />
          </Route>
        </Routes>
      );
    }

    return (
      <Routes>
        <Route element={<OperatorShell variant="minimal"><Outlet /></OperatorShell>}>
          <Route path="*" element={<CoursePortfolio />} />
        </Route>
      </Routes>
    );
  }

  if (!fullOperatorApp) {
    return (
      <Routes>
        <Route element={<OperatorShell variant="minimal"><Outlet /></OperatorShell>}>
          <Route path="*" element={<WalkUpWaitlist />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<OperatorShell variant="full"><Outlet /></OperatorShell>}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="waitlist" element={<WalkUpWaitlist />} />
        <Route path="settings" element={<TeeTimeSettings />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
    </Routes>
  );
}

export default function OperatorFeature() {
  return (
    <ThemeProvider>
      <OrgProvider>
        <CourseProvider>
          <OrgGate />
        </CourseProvider>
      </OrgProvider>
    </ThemeProvider>
  );
}
