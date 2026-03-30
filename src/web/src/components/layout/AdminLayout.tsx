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

export default function AdminLayout() {
  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex flex-col gap-3 py-2">
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-semibold font-[family-name:var(--font-heading)]">
                Shadowbrook
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
                <NavLink to="/admin/tenants">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Tenants</span>
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
        <header className="flex h-12 items-center gap-2 border-b px-4 md:hidden">
          <SidebarTrigger />
        </header>
        <Outlet />
      </SidebarInset>
    </SidebarProvider>
  );
}
