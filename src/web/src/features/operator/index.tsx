import { useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router';
import OperatorLayout from '@/components/layout/OperatorLayout';
import WaitlistShellLayout from '@/components/layout/WaitlistShellLayout';
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
        <Route element={<OperatorLayout />}>
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
          <Route element={<OperatorLayout />}>
            <Route path="*" element={<CoursePortfolio />} />
          </Route>
        </Routes>
      );
    }

    return (
      <Routes>
        <Route element={<WaitlistShellLayout />}>
          <Route path="*" element={<CoursePortfolio />} />
        </Route>
      </Routes>
    );
  }

  if (!fullOperatorApp) {
    return (
      <Routes>
        <Route element={<WaitlistShellLayout />}>
          <Route path="*" element={<WalkUpWaitlist />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<OperatorLayout />}>
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
