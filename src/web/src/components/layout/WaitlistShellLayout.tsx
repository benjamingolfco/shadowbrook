import { Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useAuth } from '@/features/auth';
import UserMenu from '@/components/layout/UserMenu';

export default function WaitlistShellLayout() {
  const { course, clearCourse } = useCourseContext();
  const { user } = useAuth();
  const navigate = useNavigate();

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  // Show course name when selected, otherwise fall back to org name
  const displayName = course?.name ?? user?.organization?.name ?? 'Teeforce';
  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-14 items-center justify-between border-b bg-sidebar px-4">
        <span className="text-lg font-semibold font-[family-name:var(--font-heading)]">
          {displayName}
        </span>
        <div className="flex items-center gap-3">
          <UserMenu onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined} />
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}
