import { Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { adminNav } from '@/features/admin/navigation';

function AdminBrand() {
  return (
    <h1 className="text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground">
      Teeforce
    </h1>
  );
}

export default function AdminLayout() {
  return (
    <AppShell variant="full" navConfig={adminNav} brand={<AdminBrand />}>
      <Outlet />
    </AppShell>
  );
}
