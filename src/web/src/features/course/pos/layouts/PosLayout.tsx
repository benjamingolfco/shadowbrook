import { Outlet } from 'react-router';
import { useCourseId } from '../../hooks/useCourseId';
import { AppShell, type NavConfig } from '@/components/layout/AppShell';
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/features/auth';

function PosBrand() {
  const { user } = useAuth();
  return (
    <>
      <h1
        className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)]"
        title={user?.organization?.name ?? 'Teeforce'}
      >
        {user?.organization?.name ?? 'Teeforce'}
      </h1>
      <Badge variant="default" className="text-[10px] px-1.5 py-0">
        POS
      </Badge>
    </>
  );
}

interface PosLayoutProps {
  variant?: 'full' | 'minimal';
}

export default function PosLayout({ variant = 'full' }: PosLayoutProps) {
  const courseId = useCourseId();

  const navConfig: NavConfig = {
    sections: [
      {
        label: 'Operations',
        items: [
          { to: `/course/${courseId}/pos/tee-sheet`, label: 'Tee Sheet' },
          { to: `/course/${courseId}/pos/waitlist`, label: 'Waitlist' },
        ],
      },
    ],
  };

  return (
    <AppShell
      variant={variant}
      navConfig={variant === 'full' ? navConfig : undefined}
      brand={<PosBrand />}
      settingsTo={variant === 'full' ? `/course/${courseId}/manage/settings` : undefined}
    >
      <Outlet />
    </AppShell>
  );
}
