import { Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
import { AppShell } from '@/components/layout/AppShell';
import { Badge } from '@/components/ui/badge';
import { ChevronsUpDown } from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useOrgContext } from '@/features/operator/context/OrgContext';
import { operatorNav } from '@/features/operator/navigation';

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
          className="flex items-center gap-1 text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground hover:bg-sidebar-accent rounded-md px-1 -mx-1"
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

function OperatorBrand() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  return (
    <>
      {isAdmin ? (
        <OrgSwitcher />
      ) : (
        <h1
          className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
          title={user?.organization?.name ?? 'Teeforce'}
        >
          {user?.organization?.name ?? 'Teeforce'}
        </h1>
      )}
      <Badge variant={isAdmin ? 'default' : 'success'} className="text-[10px] px-1.5 py-0">
        {isAdmin ? 'Admin' : 'Operator'}
      </Badge>
    </>
  );
}

export default function OperatorLayout() {
  const { user } = useAuth();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <AppShell
      variant="full"
      navConfig={operatorNav}
      brand={<OperatorBrand />}
      onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined}
    >
      <Outlet />
    </AppShell>
  );
}
