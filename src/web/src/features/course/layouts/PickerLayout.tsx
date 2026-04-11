import type { ReactNode } from 'react';
import { AppShell } from '@/components/layout/AppShell';

function PickerBrand() {
  return (
    <h1 className="text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground">
      Teeforce
    </h1>
  );
}

export default function PickerLayout({ children }: { children: ReactNode }) {
  return (
    <AppShell variant="minimal" brand={<PickerBrand />}>
      {children}
    </AppShell>
  );
}
