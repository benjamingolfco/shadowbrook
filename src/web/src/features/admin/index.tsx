import { Routes, Route, Navigate } from 'react-router';
import AdminLayout from '@/components/layout/AdminLayout';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import TenantList from './pages/TenantList';
import TenantCreate from './pages/TenantCreate';

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="tenants" element={<TenantList />} />
        <Route path="tenants/new" element={<TenantCreate />} />
        <Route path="*" element={<Navigate to="courses" replace />} />
      </Route>
    </Routes>
  );
}
