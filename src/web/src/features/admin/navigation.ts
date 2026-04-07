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
