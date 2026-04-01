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
import { Badge } from '@/components/ui/badge';
import { useAuth } from '@/features/auth/hooks/useAuth';
import CourseSwitcher from '@/features/operator/components/CourseSwitcher';
import UserMenu from '@/components/layout/UserMenu';

export default function OperatorLayout() {
  const { user } = useAuth();

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex flex-col gap-3 py-2">
            <div className="flex items-center gap-2">
              <h1
                className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)]"
                title={user?.organization?.name ?? 'Shadowbrook'}
              >
                {user?.organization?.name ?? 'Shadowbrook'}
              </h1>
              <Badge variant="success" className="text-[10px] px-1.5 py-0">
                Operator
              </Badge>
            </div>
            <CourseSwitcher />
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
        <header className="flex h-12 items-center border-b px-4">
          <SidebarTrigger className="md:hidden" />
          <div className="ml-auto">
            <UserMenu />
          </div>
        </header>
        <Outlet />
      </SidebarInset>
    </SidebarProvider>
  );
}
