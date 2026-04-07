import { Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
import { AppShell } from '@/components/layout/AppShell';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useAuth } from '@/features/auth';

function WaitlistBrand() {
  const { course } = useCourseContext();
  const { user } = useAuth();
  const displayName = course?.name ?? user?.organization?.name ?? 'Teeforce';

  return (
    <span className="text-lg font-semibold font-[family-name:var(--font-heading)] text-ink">
      {displayName}
    </span>
  );
}

export default function WaitlistShellLayout() {
  const { clearCourse } = useCourseContext();
  const { user } = useAuth();
  const navigate = useNavigate();

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <AppShell
      variant="minimal"
      brand={<WaitlistBrand />}
      onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined}
    >
      <Outlet />
    </AppShell>
  );
}
