// navigation.tsx — operator feature shell config.
//
// This file colocates `operatorNav` (the AppShell sidebar nav config) with the
// brand components (`OperatorBrand`, `WaitlistBrand`) that render in the AppShell
// header for the operator feature. The brand components are interactive (admins
// get an `OrgSwitcher` dropdown), so they are real React components rather than
// static ReactNodes — that's why this file is `.tsx`.
//
// The colocation pattern matches `features/admin/navigation.tsx` (Cluster 2),
// where `adminBrand` lives next to `adminNav`. The principle: a feature's
// nav and brand together describe one shell identity, and they belong in one place.

import { useCallback } from 'react';
import { useNavigate } from 'react-router';
import { ChevronsUpDown } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useOrgContext } from './context/OrgContext';
import { useCourseContext } from './context/CourseContext';
import type { NavConfig } from '@/components/layout/AppShell';

// eslint-disable-next-line react-refresh/only-export-components
export const operatorNav: NavConfig = {
  sections: [
    {
      label: 'Operations',
      items: [
        { to: '/operator/tee-sheet', label: 'Tee Sheet' },
        { to: '/operator/waitlist', label: 'Waitlist' },
      ],
    },
    {
      label: 'Management',
      items: [{ to: '/operator/settings', label: 'Settings' }],
    },
  ],
};

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

export function OperatorBrand() {
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

export function WaitlistBrand() {
  const { course } = useCourseContext();
  const { user } = useAuth();
  const displayName = course?.name ?? user?.organization?.name ?? 'Teeforce';

  return (
    <span className="text-lg font-semibold font-[family-name:var(--font-heading)] text-ink">
      {displayName}
    </span>
  );
}
