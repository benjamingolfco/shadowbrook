import { NavLink, Outlet } from 'react-router';
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarProvider,
  SidebarInset,
  SidebarTrigger,
} from '@/components/ui/sidebar';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/features/auth/hooks/useAuth';
import CourseSwitcher from '@/features/operator/components/CourseSwitcher';

export default function OperatorLayout() {
  const { user, logout } = useAuth();

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
        <SidebarFooter>
          <div className="flex flex-col gap-1 py-2">
            {user && (
              <p className="text-sm text-muted-foreground truncate">
                {user.displayName || user.email}
              </p>
            )}
            <Button variant="ghost" size="sm" onClick={logout} className="justify-start px-0 text-muted-foreground hover:text-foreground">
              Sign out
            </Button>
          </div>
        </SidebarFooter>
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
