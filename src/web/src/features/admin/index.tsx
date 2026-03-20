import { Routes, Route, Navigate } from 'react-router';
import AdminLayout from '@/components/layout/AdminLayout';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import TenantList from './pages/TenantList';
import TenantCreate from './pages/TenantCreate';
import TenantDetail from './pages/TenantDetail';
import DevSmsPage from '@/features/dev/pages/DevSmsPage';

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminLayout />}>
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="tenants" element={<TenantList />} />
        <Route path="tenants/new" element={<TenantCreate />} />
        <Route path="tenants/:id" element={<TenantDetail />} />
        <Route path="dev/sms" element={<DevSmsPage />} />
        <Route path="*" element={<Navigate to="tenants" replace />} />
      </Route>
    </Routes>
  );
}
