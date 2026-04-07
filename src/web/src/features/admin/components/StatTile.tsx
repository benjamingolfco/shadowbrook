import type { ReactNode } from 'react';
import { Card } from '@/components/ui/card';

export function StatTile({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Card className="border-border-strong p-4">
      <div className="text-[11px] uppercase tracking-wider text-ink-muted">{label}</div>
      <div className="mt-1 font-mono text-[28px] text-ink leading-none">{value}</div>
    </Card>
  );
}
