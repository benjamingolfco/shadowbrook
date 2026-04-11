import type { ReactNode } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';

interface WarningAlertProps {
  icon?: ReactNode;
  children: ReactNode;
}

export function WarningAlert({ icon, children }: WarningAlertProps) {
  return (
    <Alert className="border-orange-light bg-orange-faint text-foreground [&>svg]:text-orange">
      {icon}
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  );
}
