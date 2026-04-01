import { Outlet } from 'react-router';
import CourseSwitcher from '@/features/operator/components/CourseSwitcher';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import UserMenu from '@/components/layout/UserMenu';

export default function WaitlistShellLayout() {
  const { course } = useCourseContext();

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-14 items-center justify-between border-b bg-sidebar px-4">
        <div className="flex items-center gap-3">
          <span className="text-lg font-semibold font-[family-name:var(--font-heading)]">
            {course?.name ?? 'Shadowbrook'}
          </span>
          <CourseSwitcher />
        </div>
        <div className="flex items-center gap-3">
          <UserMenu />
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}
