import { Routes, Route, Navigate } from 'react-router';
import { ThemeProvider } from '@/components/ThemeProvider';
import ManagementLayout from './manage/layouts/ManagementLayout';
import PosLayout from './pos/layouts/PosLayout';
import Schedule from './manage/pages/Schedule';
import Settings from './manage/pages/Settings';
import TeeSheet from './pos/pages/TeeSheet';
import WalkUpWaitlist from './pos/pages/WalkUpWaitlist';

// Dashboard, ScheduleDay will be added in later tasks
function PlaceholderPage({ name }: { name: string }) {
  return <div className="p-6 text-muted-foreground">{name} — coming soon</div>;
}

export default function CourseFeature() {
  return (
    <ThemeProvider>
      <Routes>
        <Route path="manage" element={<ManagementLayout />}>
          <Route index element={<PlaceholderPage name="Dashboard" />} />
          <Route path="schedule" element={<Schedule />} />
          <Route path="schedule/:date" element={<PlaceholderPage name="Day Detail" />} />
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
    </ThemeProvider>
  );
}
