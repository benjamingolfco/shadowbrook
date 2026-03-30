import { Link, useNavigate } from 'react-router';
import { useTenants } from '../hooks/useTenants';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
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
  const { data: tenants, isLoading, error, refetch } = useTenants();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="p-6 space-y-6">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {[1, 2, 3].map((i) => (
            <Card key={i}>
              <CardHeader>
                <Skeleton className="h-4 w-24" />
              </CardHeader>
              <CardContent>
                <Skeleton className="h-8 w-16" />
              </CardContent>
            </Card>
          ))}
        </div>
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-12 w-full" />
          ))}
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 space-y-4">
        <p className="text-destructive">
          {error instanceof Error ? error.message : 'Failed to load tenants'}
        </p>
        <Button variant="outline" onClick={() => refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const totalTenants = tenants?.length ?? 0;
  const totalCourses = tenants?.reduce((sum, t) => sum + (t.courseCount ?? 0), 0) ?? 0;
  const tenantsWithNoCourses = tenants?.filter((t) => (t.courseCount ?? 0) === 0).length ?? 0;

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">All Registered Tenants</h1>
          <p className="text-sm text-muted-foreground">Platform Admin View</p>
        </div>
        <Button asChild>
          <Link to="/admin/tenants/new">Register Tenant</Link>
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card aria-label="Total tenants">
          <CardHeader>
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total Tenants
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">{totalTenants}</div>
          </CardContent>
        </Card>
        <Card aria-label="Total courses">
          <CardHeader>
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Total Courses
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">{totalCourses}</div>
          </CardContent>
        </Card>
        <Card aria-label="Tenants without courses">
          <CardHeader>
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Tenants Without Courses
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">{tenantsWithNoCourses}</div>
          </CardContent>
        </Card>
      </div>

      {!tenants || tenants.length === 0 ? (
        <div role="status" className="text-center py-12">
          <p className="text-muted-foreground text-lg">No tenants registered yet.</p>
          <p className="text-muted-foreground text-sm mt-2">
            Register your first tenant to get started.
          </p>
        </div>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Organization Name</TableHead>
                <TableHead>Contact Name</TableHead>
                <TableHead className="hidden md:table-cell">Contact Info</TableHead>
                <TableHead>Courses</TableHead>
                <TableHead>Registered</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {tenants.map((tenant: Tenant) => (
                <TableRow
                  key={tenant.id}
                  className="cursor-pointer hover:bg-muted/50"
                  role="link"
                  onClick={() => navigate(`/admin/tenants/${tenant.id}`)}
                >
                  <TableCell>
                    <Link
                      to={`/admin/tenants/${tenant.id}`}
                      className="font-semibold text-primary hover:underline"
                      onClick={(e) => e.stopPropagation()}
                    >
                      {tenant.organizationName}
                    </Link>
                  </TableCell>
                  <TableCell>{tenant.contactName}</TableCell>
                  <TableCell className="hidden md:table-cell">
                    <div className="space-y-0.5">
                      <div className="text-sm">{tenant.contactEmail}</div>
                      <div className="text-sm text-muted-foreground">{tenant.contactPhone}</div>
                    </div>
                  </TableCell>
                  <TableCell>
                    {tenant.courseCount !== undefined ? (
                      tenant.courseCount === 0 ? (
                        <Badge variant="secondary">No courses</Badge>
                      ) : (
                        <span>{tenant.courseCount}</span>
                      )
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
