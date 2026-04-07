import { Routes, Route, Navigate, Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { adminNav, adminBrand } from './navigation';
import Dashboard from './pages/Dashboard';
import OrgList from './pages/OrgList';
import OrgCreate from './pages/OrgCreate';
import OrgDetail from './pages/OrgDetail';
import CourseList from './pages/CourseList';
import CourseCreate from './pages/CourseCreate';
import CourseDetail from './pages/CourseDetail';
import UserList from './pages/UserList';
import UserCreate from './pages/UserCreate';
import UserDetail from './pages/UserDetail';
import FeatureFlags from './pages/FeatureFlags';
import DeadLetters from './pages/DeadLetters';
import DevSmsPage from '@/features/dev/pages/DevSmsPage';

function AdminShell() {
  return (
    <AppShell variant="full" navConfig={adminNav} brand={adminBrand}>
      <Outlet />
    </AppShell>
  );
}

export default function AdminFeature() {
  return (
    <Routes>
      <Route element={<AdminShell />}>
        <Route index element={<Dashboard />} />
        <Route path="organizations" element={<OrgList />} />
        <Route path="organizations/new" element={<OrgCreate />} />
        <Route path="organizations/:id" element={<OrgDetail />} />
        <Route path="courses" element={<CourseList />} />
        <Route path="courses/new" element={<CourseCreate />} />
        <Route path="courses/:id" element={<CourseDetail />} />
        <Route path="users" element={<UserList />} />
        <Route path="users/new" element={<UserCreate />} />
        <Route path="users/:id" element={<UserDetail />} />
        <Route path="feature-flags" element={<FeatureFlags />} />
        <Route path="dead-letters" element={<DeadLetters />} />
        {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && <Route path="dev/sms" element={<DevSmsPage />} />}
        <Route path="*" element={<Navigate to="/admin" replace />} />
      </Route>
    </Routes>
  );
}
