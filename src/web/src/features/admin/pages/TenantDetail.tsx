import { useParams, Link } from 'react-router';
import { useTenant } from '../hooks/useTenants';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

export default function TenantDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: tenant, isLoading, error } = useTenant(id!);

  if (isLoading) {
    return (
      <div className="p-6 space-y-6">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-8 w-64" />
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  if (error) {
    const status = (error as Error & { status?: number }).status;
    if (status === 404) {
      return (
        <div className="p-6 space-y-4">
          <p className="text-muted-foreground text-lg">Tenant not found</p>
          <p className="text-muted-foreground text-sm">
            The tenant you are looking for does not exist or has been removed.
          </p>
          <Button variant="outline" asChild>
            <Link to="/admin/tenants">Back to Tenants</Link>
          </Button>
        </div>
      );
    }
    return (
      <div className="p-6 space-y-4">
        <p className="text-destructive">
          {error instanceof Error ? error.message : 'Failed to load tenant'}
        </p>
        <Button variant="outline" asChild>
          <Link to="/admin/tenants">Back to Tenants</Link>
        </Button>
      </div>
    );
  }

  if (!tenant) {
    return null;
  }

  return (
    <div className="p-6 space-y-6">
      {/* Back navigation */}
      <Button variant="ghost" size="sm" asChild>
        <Link to="/admin/tenants">← Back to Tenants</Link>
      </Button>

      {/* Tenant header */}
      <div>
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">{tenant.organizationName}</h1>
        <p className="text-sm text-muted-foreground">
          Registered {new Date(tenant.createdAt).toLocaleDateString()}
        </p>
      </div>

      {/* Contact info grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <p className="text-sm font-medium text-muted-foreground">Contact Name</p>
          <p>{tenant.contactName}</p>
        </div>
        <div>
          <p className="text-sm font-medium text-muted-foreground">Contact Email</p>
          <p>{tenant.contactEmail}</p>
        </div>
        <div>
          <p className="text-sm font-medium text-muted-foreground">Contact Phone</p>
          <p>{tenant.contactPhone}</p>
        </div>
      </div>

      {/* Courses section */}
      <div>
        <h2 className="text-lg font-semibold font-[family-name:var(--font-heading)] mb-4">Courses ({tenant.courses.length})</h2>

        {tenant.courses.length === 0 ? (
          <div role="status" className="text-center py-8 border rounded-md">
            <p className="text-muted-foreground">No courses assigned to this tenant yet.</p>
          </div>
        ) : (
          <div className="border rounded-md">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Location</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {tenant.courses.map((course) => (
                  <TableRow key={course.id}>
                    <TableCell className="font-semibold">{course.name}</TableCell>
                    <TableCell>
                      {course.city || course.state ? (
                        <span>
                          {course.city}
                          {course.city && course.state ? ', ' : ''}
                          {course.state}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">—</span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </div>
    </div>
  );
}
