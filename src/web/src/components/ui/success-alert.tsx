import type { ReactNode } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';

interface SuccessAlertProps {
  icon?: ReactNode;
  children: ReactNode;
}

export function SuccessAlert({ icon, children }: SuccessAlertProps) {
  return (
    <Alert className="text-success border-success/30 *:data-[slot=alert-description]:text-success/90 [&>svg]:text-current">
      {icon}
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  );
}
