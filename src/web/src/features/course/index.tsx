import { Routes, Route, Navigate } from 'react-router';
import { ThemeProvider } from '@/components/ThemeProvider';
import { OrgProvider } from './context/OrgContext';
import { CourseProvider } from './context/CourseProvider';
import { useFeature } from '@/hooks/use-features';
import { useCourseId } from './hooks/useCourseId';
import PickerLayout from './layouts/PickerLayout';
import ManagementLayout from './manage/layouts/ManagementLayout';
import PosLayout from './pos/layouts/PosLayout';
import Dashboard from './manage/pages/Dashboard';
import Schedule from './manage/pages/Schedule';
import ScheduleDay from './manage/pages/ScheduleDay';
import Settings from './manage/pages/Settings';
import TeeSheet from './pos/pages/TeeSheet';
import WalkUpWaitlist from './pos/pages/WalkUpWaitlist';
import CoursePicker from './pages/CoursePicker';

function CourseRoutes() {
  const courseId = useCourseId();
  const fullApp = useFeature('full-operator-app', courseId);

  if (!fullApp) {
    return (
      <Routes>
        <Route path="pos" element={<PosLayout variant="minimal" />}>
          <Route path="waitlist" element={<WalkUpWaitlist />} />
          <Route path="*" element={<Navigate to="waitlist" replace />} />
        </Route>
        <Route path="*" element={<Navigate to="pos/waitlist" replace />} />
      </Routes>
    );
  }

  return (
    <Routes>
      <Route path="manage" element={<ManagementLayout />}>
        <Route index element={<Dashboard />} />
        <Route path="schedule" element={<Schedule />} />
        <Route path="schedule/:date" element={<ScheduleDay />} />
        <Route path="settings" element={<Settings />} />
      </Route>
      <Route path="pos" element={<PosLayout />}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="waitlist" element={<WalkUpWaitlist />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
      <Route index element={<Navigate to="manage" replace />} />
      <Route path="*" element={<Navigate to="manage" replace />} />
    </Routes>
  );
}

function CourseWithProvider() {
  return (
    <CourseProvider>
      <CourseRoutes />
    </CourseProvider>
  );
}

export default function CourseFeature() {
  return (
    <ThemeProvider>
      <OrgProvider>
        <Routes>
          <Route index element={<PickerLayout><CoursePicker /></PickerLayout>} />
          <Route path=":courseId/*" element={<CourseWithProvider />} />
        </Routes>
      </OrgProvider>
    </ThemeProvider>
  );
}
