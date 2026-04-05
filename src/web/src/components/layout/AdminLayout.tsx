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
import UserMenu from '@/components/layout/UserMenu';

export default function AdminLayout() {
  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex flex-col gap-3 py-2">
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-semibold font-[family-name:var(--font-heading)]">
                Teeforce
              </h1>
              <Badge variant="outline" className="text-[10px] px-1.5 py-0">
                Admin
              </Badge>
            </div>
          </div>
        </SidebarHeader>
        <SidebarContent>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/admin" end>
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Dashboard</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/admin/organizations">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Organizations</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/admin/courses">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Courses</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/admin/users">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Users</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/admin/feature-flags">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Feature Flags</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/admin/dead-letters">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Dead Letters</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && (
              <SidebarMenuItem>
                <SidebarMenuButton asChild>
                  <NavLink to="/admin/dev/sms">
                    {({ isActive }) => (
                      <span className={isActive ? 'font-semibold' : ''}>SMS Log</span>
                    )}
                  </NavLink>
                </SidebarMenuButton>
              </SidebarMenuItem>
            )}
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
