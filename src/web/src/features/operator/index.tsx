import { Routes, Route, Navigate } from 'react-router';
import OperatorLayout from '@/components/layout/OperatorLayout';
import TeeSheet from './pages/TeeSheet';
import TeeTimeSettings from './pages/TeeTimeSettings';
import CourseRegister from './pages/CourseRegister';
import OrganizationSelect from './pages/OrganizationSelect';
import { TenantProvider, useTenantContext } from './context/TenantContext';

function OperatorRoutes() {
  const { tenant } = useTenantContext();

  if (!tenant) {
    return <OrganizationSelect />;
  }

  return (
    <Routes>
      <Route element={<OperatorLayout />}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="settings" element={<TeeTimeSettings />} />
        <Route path="register-course" element={<CourseRegister />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
    </Routes>
  );
}

export default function OperatorFeature() {
  return (
    <TenantProvider>
      <OperatorRoutes />
    </TenantProvider>
  );
}
