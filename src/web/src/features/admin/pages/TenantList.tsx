import { Link } from 'react-router';
import { useTenants } from '../hooks/useTenants';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import type { Tenant } from '@/types/tenant';

export default function TenantList() {
  const { data: tenants, isLoading, error } = useTenants();

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading tenants...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load tenants'}
        </p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">All Registered Tenants</h1>
          <p className="text-sm text-muted-foreground">Platform Admin View</p>
        </div>
        <Button asChild>
          <Link to="/admin/tenants/new">Register Tenant</Link>
        </Button>
      </div>

      {!tenants || tenants.length === 0 ? (
        <p className="text-muted-foreground">No tenants registered yet.</p>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Organization Name</TableHead>
                <TableHead>Contact Name</TableHead>
                <TableHead>Contact Info</TableHead>
                <TableHead>Courses</TableHead>
                <TableHead>Registered</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tenants.map((tenant: Tenant) => (
                <TableRow key={tenant.id}>
                  <TableCell className="font-semibold">{tenant.organizationName}</TableCell>
                  <TableCell>{tenant.contactName}</TableCell>
                  <TableCell>
                    <div className="space-y-0.5">
                      <div className="text-sm">{tenant.contactEmail}</div>
                      <div className="text-sm text-muted-foreground">{tenant.contactPhone}</div>
                    </div>
                  </TableCell>
                  <TableCell>
                    {tenant.courseCount !== undefined ? (
                      <span className={tenant.courseCount === 0 ? 'text-muted-foreground' : ''}>
                        {tenant.courseCount === 0 ? 'None' : tenant.courseCount}
                      </span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    {new Date(tenant.createdAt).toLocaleDateString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
