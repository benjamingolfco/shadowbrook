import { Routes, Route, Navigate } from 'react-router';
import OperatorLayout from '@/components/layout/OperatorLayout';
import WaitlistShellLayout from '@/components/layout/WaitlistShellLayout';
import TeeSheet from './pages/TeeSheet';
import TeeTimeSettings from './pages/TeeTimeSettings';
import WalkUpWaitlist from './pages/WalkUpWaitlist';
import CoursePortfolio from './pages/CoursePortfolio';
import { CourseProvider, useCourseContext } from './context/CourseContext';
import { ThemeProvider } from '@/components/ThemeProvider';
import { useFeature } from '@/hooks/use-features';

function CourseGate() {
  const { course } = useCourseContext();
  const fullOperatorApp = useFeature('full-operator-app', course?.id);

  if (!course) {
    return (
      <Routes>
        <Route element={<OperatorLayout />}>
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
      <CourseProvider>
        <CourseGate />
      </CourseProvider>
    </ThemeProvider>
  );
}
