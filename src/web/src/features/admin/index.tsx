import { Routes, Route, Navigate } from 'react-router';
import AdminLayout from '@/components/layout/AdminLayout';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="*" element={<Navigate to="courses" replace />} />
      </Route>
    </Routes>
  );
}
