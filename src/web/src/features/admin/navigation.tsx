import type { ReactNode } from 'react';
import type { NavConfig } from '@/components/layout/AppShell';

const isDevMode = import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true';

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
    ...(isDevMode
      ? [
          {
            label: 'Dev Tools',
            items: [{ to: '/admin/dev/sms', label: 'SMS Viewer' }],
          },
        ]
      : []),
  ],
};

export const adminBrand: ReactNode = (
  <span className="text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground">Teeforce</span>
);
