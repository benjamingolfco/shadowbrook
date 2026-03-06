import type { ReactNode } from 'react';
import { Button } from '@/components/ui/button';

export interface PageAction {
  id: string;
  label: string;
  description?: string;
  onClick: () => void;
  variant?: 'default' | 'outline' | 'destructive';
  disabled?: boolean;
  disabledLabel?: string;
}

interface PageHeaderProps {
  title: string;
  actions?: PageAction[];
  children?: ReactNode;
}

export function PageHeader({ title, actions, children }: PageHeaderProps) {
  return (
    <div className="mb-6">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-bold">{title}</h1>
          {children && <div className="mt-1">{children}</div>}
        </div>
        {actions && actions.length > 0 && (
          <div className="flex items-center gap-2 shrink-0">
            {actions.map((action) => (
              <Button
                key={action.id}
                variant={action.variant}
                disabled={action.disabled}
                onClick={action.onClick}
                title={action.description}
              >
                {action.disabled && action.disabledLabel
                  ? action.disabledLabel
                  : action.label}
              </Button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
