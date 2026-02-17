import { useTenants } from '@/hooks/useTenants';
import { useTenantContext } from '../context/TenantContext';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';

export default function OrganizationSelect() {
  const { data: tenants, isLoading, error } = useTenants();
  const { selectTenant } = useTenantContext();

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="w-full max-w-2xl space-y-6 p-8">
          <h1 className="text-2xl font-bold">Select Organization</h1>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Organization Name</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {[...Array(3)].map((_, i) => (
                  <TableRow key={i}>
                    <TableCell>
                      <Skeleton className="h-6 w-48" />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="w-full max-w-2xl space-y-6 p-8">
          <h1 className="text-2xl font-bold">Select Organization</h1>
          <p className="text-destructive">
            Error loading organizations: {error instanceof Error ? error.message : 'Unknown error'}
          </p>
        </div>
      </div>
    );
  }

  if (!tenants || tenants.length === 0) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="w-full max-w-2xl space-y-6 p-8">
          <h1 className="text-2xl font-bold">Select Organization</h1>
          <p className="text-muted-foreground">
            No organizations registered yet. Please contact your administrator or register a tenant
            via the Admin view.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen items-center justify-center">
      <div className="w-full max-w-2xl space-y-6 p-8">
        <h1 className="text-2xl font-bold">Select Organization</h1>
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Organization Name</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tenants.map((tenant) => (
                <TableRow
                  key={tenant.id}
                  className="cursor-pointer hover:bg-muted"
                  onClick={() =>
                    selectTenant({ id: tenant.id, organizationName: tenant.organizationName })
                  }
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      selectTenant({ id: tenant.id, organizationName: tenant.organizationName });
                    }
                  }}
                >
                  <TableCell>{tenant.organizationName}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  );
}
