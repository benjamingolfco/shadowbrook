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

export default function AdminLayout() {
  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex flex-col gap-2">
            <h1 className="text-lg font-bold">Shadowbrook</h1>
            <span className="inline-flex items-center rounded-md bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">
              Admin
            </span>
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
