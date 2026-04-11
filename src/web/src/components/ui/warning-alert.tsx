import type { ReactNode } from 'react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { cn } from '@/lib/utils';

interface WarningAlertProps {
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function WarningAlert({ icon, children, className }: WarningAlertProps) {
  return (
    <Alert className={cn("border-orange-light bg-orange-faint text-foreground [&>svg]:text-orange", className)}>
      {icon}
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  );
}
