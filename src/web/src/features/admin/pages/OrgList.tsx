import { Link, useNavigate } from 'react-router';
import { useOrganizations } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { StatTile } from '../components/StatTile';
import type { Organization } from '@/types/organization';

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

  const loadingSkeleton = <Skeleton className="h-7 w-12 inline-block" />;

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-[family-name:var(--font-heading)] text-[18px] text-ink">Organizations</h1>}
        right={
          <Button asChild>
            <Link to="/admin/organizations/new">Create Organization</Link>
          </Button>
        }
      />

      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-6">
        <StatTile label="Total Organizations" value={isLoading ? loadingSkeleton : (orgs?.length ?? 0)} />
        <StatTile label="Total Courses" value={isLoading ? loadingSkeleton : totalCourses} />
        <StatTile label="Total Users" value={isLoading ? loadingSkeleton : totalUsers} />
      </div>

      {error && (
        <div className="flex items-center gap-4 mb-6">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Failed to load organizations'}
          </p>
          <Button variant="outline" size="sm" onClick={() => void refetch()}>
            Retry
          </Button>
        </div>
      )}

      {isLoading ? (
        <div className="border border-border-strong rounded-md bg-card overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Courses</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Users</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Created</TableHead>
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
        <p className="text-ink-muted text-sm py-12 text-center">No organizations yet. Create one to get started.</p>
      ) : (
        <div className="border border-border-strong rounded-md bg-card overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Courses</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Users</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {orgs.map((org: Organization) => (
                <TableRow
                  key={org.id}
                  className="cursor-pointer"
                  onClick={() => navigate(`/admin/organizations/${org.id}`)}
                >
                  <TableCell className="font-medium">{org.name}</TableCell>
                  <TableCell className="font-mono text-[13px] text-ink">{org.courseCount}</TableCell>
                  <TableCell className="font-mono text-[13px] text-ink">{org.userCount}</TableCell>
                  <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                    {new Date(org.createdAt).toLocaleDateString()}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
