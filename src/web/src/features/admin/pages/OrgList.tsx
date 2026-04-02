import { Link, useNavigate } from 'react-router';
import { useOrganizations } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
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
import type { Organization } from '@/types/organization';

function SummaryCardSkeleton() {
  return (
    <Card>
      <CardHeader className="pb-2">
        <Skeleton className="h-4 w-32" />
      </CardHeader>
      <CardContent>
        <Skeleton className="h-8 w-16" />
      </CardContent>
    </Card>
  );
}

function TableRowSkeleton() {
  return (
    <TableRow>
      <TableCell><Skeleton className="h-4 w-40" /></TableCell>
      <TableCell><Skeleton className="h-4 w-12" /></TableCell>
      <TableCell><Skeleton className="h-4 w-12" /></TableCell>
      <TableCell className="hidden md:table-cell"><Skeleton className="h-4 w-24" /></TableCell>
    </TableRow>
  );
}

export default function OrgList() {
  const { data: orgs, isLoading, error, refetch } = useOrganizations();
  const navigate = useNavigate();

  const totalCourses = orgs?.reduce((sum, org) => sum + org.courseCount, 0) ?? 0;
  const totalUsers = orgs?.reduce((sum, org) => sum + org.userCount, 0) ?? 0;

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Organizations</h1>
          <p className="text-sm text-muted-foreground">Platform Admin View</p>
        </div>
        <Button asChild>
          <Link to="/admin/organizations/new">Create Organization</Link>
        </Button>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
        {isLoading ? (
          <>
            <SummaryCardSkeleton />
            <SummaryCardSkeleton />
            <SummaryCardSkeleton />
          </>
        ) : (
          <>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Total Organizations</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold">{orgs?.length ?? 0}</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Total Courses</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold">{totalCourses}</p>
              </CardContent>
            </Card>
            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">Total Users</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-bold">{totalUsers}</p>
              </CardContent>
            </Card>
          </>
        )}
      </div>

      {error && (
        <div className="flex items-center gap-4">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Failed to load organizations'}
          </p>
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            Retry
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Courses</TableHead>
                <TableHead>Users</TableHead>
                <TableHead className="hidden md:table-cell">Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {Array.from({ length: 5 }).map((_, i) => (
                <TableRowSkeleton key={i} />
              ))}
            </TableBody>
          </Table>
        </div>
      ) : !orgs || orgs.length === 0 ? (
        <p className="text-muted-foreground">No organizations yet. Create one to get started.</p>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Courses</TableHead>
                <TableHead>Users</TableHead>
                <TableHead className="hidden md:table-cell">Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orgs.map((org: Organization) => (
                <TableRow
                  key={org.id}
                  className="cursor-pointer"
                  onClick={() => navigate(`/admin/organizations/${org.id}`)}
                >
                  <TableCell className="font-semibold">{org.name}</TableCell>
                  <TableCell>{org.courseCount}</TableCell>
                  <TableCell>{org.userCount}</TableCell>
                  <TableCell className="hidden md:table-cell text-sm">
                    {new Date(org.createdAt).toLocaleDateString()}
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
