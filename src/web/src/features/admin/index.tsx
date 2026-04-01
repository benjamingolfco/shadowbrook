import { Routes, Route, Navigate } from 'react-router';
import AdminLayout from '@/components/layout/AdminLayout';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import DevSmsPage from '@/features/dev/pages/DevSmsPage';

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && <Route path="dev/sms" element={<DevSmsPage />} />}
        <Route path="*" element={<Navigate to="courses" replace />} />
      </Route>
    </Routes>
  );
}
