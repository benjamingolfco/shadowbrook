import { Outlet } from 'react-router';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/features/auth/hooks/useAuth';
import CourseSwitcher from '@/features/operator/components/CourseSwitcher';
import { useCourseContext } from '@/features/operator/context/CourseContext';

export default function WaitlistShellLayout() {
  const { user, logout } = useAuth();
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
          {user && (
            <span className="text-sm text-muted-foreground">
              {user.displayName || user.email}
            </span>
          )}
          <Button variant="ghost" size="sm" onClick={logout} className="text-muted-foreground hover:text-foreground">
            Sign out
          </Button>
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}
