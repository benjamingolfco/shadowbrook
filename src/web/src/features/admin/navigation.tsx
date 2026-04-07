import type { ReactNode } from 'react';
import type { NavConfig } from '@/components/layout/AppShell';

export const adminNav: NavConfig = {
  sections: [
    {
      label: 'Platform',
      items: [
        { to: '/admin', label: 'Dashboard' },
        { to: '/admin/organizations', label: 'Organizations' },
        { to: '/admin/courses', label: 'Courses' },
        { to: '/admin/users', label: 'Users' },
      ],
    },
    {
      label: 'System',
      items: [
        { to: '/admin/feature-flags', label: 'Feature Flags' },
        { to: '/admin/dead-letters', label: 'Dead Letters' },
      ],
    },
  ],
};

export const adminBrand: ReactNode = (
  <span className="text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground">Teeforce</span>
);
