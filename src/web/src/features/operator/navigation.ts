import type { NavConfig } from '@/components/layout/AppShell';

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
      items: [
        { to: '/operator/settings', label: 'Settings' },
      ],
    },
  ],
};
