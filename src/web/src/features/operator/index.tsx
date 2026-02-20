import { Routes, Route, Navigate, useLocation } from 'react-router';
import OperatorLayout from '@/components/layout/OperatorLayout';
import TeeSheet from './pages/TeeSheet';
import TeeTimeSettings from './pages/TeeTimeSettings';
import CourseRegister from './pages/CourseRegister';
import OrganizationSelect from './pages/OrganizationSelect';
import CoursePortfolio from './pages/CoursePortfolio';
import { TenantProvider, useTenantContext } from './context/TenantContext';
import { CourseProvider, useCourseContext } from './context/CourseContext';

function CourseGate() {
  const { course } = useCourseContext();
  const location = useLocation();

  // Allow register-course route without a course selected
  if (!course) {
    if (location.pathname === '/operator/register-course') {
      return (
        <Routes>
          <Route element={<OperatorLayout />}>
            <Route path="register-course" element={<CourseRegister />} />
          </Route>
        </Routes>
      );
    }
    return <CoursePortfolio />;
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

function TenantGate() {
  const { tenant } = useTenantContext();

  if (!tenant) {
    return <OrganizationSelect />;
  }

  return (
    <CourseProvider key={tenant.id} tenantId={tenant.id}>
      <CourseGate />
    </CourseProvider>
  );
}

export default function OperatorFeature() {
  return (
    <TenantProvider>
      <TenantGate />
    </TenantProvider>
  );
}
