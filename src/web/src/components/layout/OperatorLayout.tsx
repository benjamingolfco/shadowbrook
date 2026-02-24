import { NavLink, Outlet } from 'react-router';
import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarProvider,
  SidebarInset,
  SidebarTrigger,
} from '@/components/ui/sidebar';
import { Button } from '@/components/ui/button';
import { useTenantContext } from '@/features/operator/context/TenantContext';
import CourseSwitcher from '@/features/operator/components/CourseSwitcher';

export default function OperatorLayout() {
  const { tenant, clearTenant } = useTenantContext();

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex flex-col gap-2">
            <h1
              className="max-w-[200px] truncate text-lg font-bold"
              title={tenant?.organizationName ?? 'Shadowbrook'}
            >
              {tenant?.organizationName ?? 'Shadowbrook'}
            </h1>
            <span className="inline-flex items-center rounded-md bg-green-50 px-2 py-1 text-xs font-medium text-green-700 ring-1 ring-inset ring-green-700/10">
              Operator
            </span>
            <CourseSwitcher />
            {tenant && (
              <Button variant="ghost" size="sm" onClick={clearTenant}>
                Change Organization
              </Button>
            )}
          </div>
        </SidebarHeader>
        <SidebarContent>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/tee-sheet">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Tee Sheet</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/settings">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Settings</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/register-course">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Register Course</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarContent>
      </Sidebar>
      <SidebarInset>
        <header className="flex h-12 items-center gap-2 border-b px-4 md:hidden">
          <SidebarTrigger />
        </header>
        <Outlet />
      </SidebarInset>
    </SidebarProvider>
  );
}
