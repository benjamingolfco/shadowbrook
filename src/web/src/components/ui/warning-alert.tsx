import type { ReactNode } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';

export function WarningAlert({ children }: { children: ReactNode }) {
  return (
    <Alert className="border-orange-light bg-orange-faint text-foreground [&>svg]:text-orange">
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  );
}
