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
import { Badge } from '@/components/ui/badge';
import { useTenantContext } from '@/features/operator/context/TenantContext';
import CourseSwitcher from '@/features/operator/components/CourseSwitcher';

export default function OperatorLayout() {
  const { tenant, clearTenant } = useTenantContext();

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex flex-col gap-3 py-2">
            <div className="flex items-center gap-2">
              <h1
                className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)]"
                title={tenant?.organizationName ?? 'Shadowbrook'}
              >
                {tenant?.organizationName ?? 'Shadowbrook'}
              </h1>
              <Badge variant="success" className="text-[10px] px-1.5 py-0">
                Operator
              </Badge>
            </div>
            <CourseSwitcher />
            {tenant && (
              <Button variant="ghost" size="sm" onClick={clearTenant} className="justify-start px-0 text-muted-foreground hover:text-foreground">
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
                <NavLink to="/operator/waitlist">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Waitlist</span>
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
