import { NavLink, Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
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
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ChevronsUpDown } from 'lucide-react';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useOrgContext } from '@/features/operator/context/OrgContext';
import UserMenu from '@/components/layout/UserMenu';

function OrgSwitcher() {
  const { organizations } = useAuth();
  const { org, selectOrg, clearOrg } = useOrgContext();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const handleSelect = useCallback(
    (selected: { id: string; name: string }) => {
      clearCourse();
      selectOrg({ id: selected.id, name: selected.name });
      navigate('/operator');
    },
    [clearCourse, selectOrg, navigate],
  );

  const handleClear = useCallback(() => {
    clearCourse();
    clearOrg();
    navigate('/operator');
  }, [clearCourse, clearOrg, navigate]);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1 text-lg font-semibold font-[family-name:var(--font-heading)] hover:bg-accent rounded-md px-1 -mx-1"
        >
          <span className="max-w-[180px] truncate" title={org?.name ?? 'Select org'}>
            {org?.name ?? 'Select org'}
          </span>
          <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        {organizations.map((o) => (
          <DropdownMenuItem
            key={o.id}
            onSelect={() => handleSelect(o)}
            className={o.id === org?.id ? 'bg-accent' : ''}
          >
            {o.name}
          </DropdownMenuItem>
        ))}
        {org && (
          <DropdownMenuItem onSelect={handleClear} className="text-muted-foreground">
            Back to org list
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default function OperatorLayout() {
  const { user } = useAuth();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();
  const isAdmin = user?.role === 'Admin';

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex items-center gap-2 py-2">
            {isAdmin ? (
              <OrgSwitcher />
            ) : (
              <h1
                className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)]"
                title={user?.organization?.name ?? 'Shadowbrook'}
              >
                {user?.organization?.name ?? 'Shadowbrook'}
              </h1>
            )}
            <Badge variant={isAdmin ? 'default' : 'success'} className="text-[10px] px-1.5 py-0">
              {isAdmin ? 'Admin' : 'Operator'}
            </Badge>
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
          </SidebarMenu>
        </SidebarContent>
      </Sidebar>
      <SidebarInset>
        <header className="flex h-12 items-center border-b px-4">
          <SidebarTrigger className="md:hidden" />
          <div className="ml-auto">
            <UserMenu onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined} />
          </div>
        </header>
        <Outlet />
      </SidebarInset>
    </SidebarProvider>
  );
}
