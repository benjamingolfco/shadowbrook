import { Alert, AlertDescription } from '@/components/ui/alert';

export function WarningAlert({ children }: { children: React.ReactNode }) {
  return (
    <Alert className="border-amber-200 bg-amber-50 text-amber-900 [&>svg]:text-amber-600">
      <AlertDescription>{children}</AlertDescription>
    </Alert>
  );
}
