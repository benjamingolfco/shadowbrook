import { Routes, Route, Navigate } from 'react-router';
import { ThemeProvider } from '@/components/ThemeProvider';

function PlaceholderPage({ name }: { name: string }) {
  return <div className="p-6 text-muted-foreground">{name} — coming soon</div>;
}

export default function CourseFeature() {
  return (
    <ThemeProvider>
      <Routes>
        <Route path="manage">
          <Route index element={<PlaceholderPage name="Dashboard" />} />
          <Route path="schedule" element={<PlaceholderPage name="Schedule" />} />
          <Route path="schedule/:date" element={<PlaceholderPage name="Day Detail" />} />
          <Route path="settings" element={<PlaceholderPage name="Settings" />} />
        </Route>
        <Route path="pos">
          <Route path="tee-sheet" element={<PlaceholderPage name="Tee Sheet" />} />
          <Route path="waitlist" element={<PlaceholderPage name="Waitlist" />} />
          <Route path="*" element={<Navigate to="tee-sheet" replace />} />
        </Route>
        <Route index element={<Navigate to="manage" replace />} />
        <Route path="*" element={<Navigate to="manage" replace />} />
      </Routes>
    </ThemeProvider>
  );
}
