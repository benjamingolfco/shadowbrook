import { Outlet } from 'react-router';
import { useCourseId } from '../../hooks/useCourseId';
import { AppShell, type NavConfig } from '@/components/layout/AppShell';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/features/auth';

function ManagementBrand() {
  const { user } = useAuth();
  return (
    <>
      <h1
        className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
        title={user?.organization?.name ?? 'Teeforce'}
      >
        {user?.organization?.name ?? 'Teeforce'}
      </h1>
      <Badge variant="success" className="text-[10px] px-1.5 py-0">
        Manage
      </Badge>
    </>
  );
}

export default function ManagementLayout() {
  const courseId = useCourseId();

  const navConfig: NavConfig = {
    sections: [
      {
        label: 'Management',
        items: [
          { to: `/course/${courseId}/manage`, label: 'Dashboard' },
          { to: `/course/${courseId}/manage/schedule`, label: 'Schedule' },
          { to: `/course/${courseId}/manage/settings`, label: 'Settings' },
        ],
      },
    ],
  };

  return (
    <AppShell variant="full" navConfig={navConfig} brand={<ManagementBrand />} settingsTo={`/course/${courseId}/manage/settings`}>
      <Outlet />
    </AppShell>
  );
}
