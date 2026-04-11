import { Link, useNavigate } from 'react-router';
import { useUsers, useInviteUser } from '../hooks/useUsers';
import { useOrganizations } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { PageTopbar } from '@/components/layout/PageTopbar';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { MoreHorizontal } from 'lucide-react';

export default function UserList() {
  const { data: users, isLoading, error } = useUsers();
  const { data: orgs } = useOrganizations();
  const orgMap = new Map(orgs?.map(o => [o.id, o.name]) ?? []);
  const navigate = useNavigate();
  const inviteUser = useInviteUser();

  const topbar = (
    <PageTopbar
      middle={<h1 className="font-[family-name:var(--font-heading)] text-[18px] text-ink">Users</h1>}
      right={
        <Button asChild>
          <Link to="/admin/users/new">Create User</Link>
        </Button>
      }
    />
  );

  if (isLoading) {
    return (
      <>
        {topbar}
        <p className="text-ink-muted">Loading users...</p>
      </>
    );
  }

  if (error) {
    return (
      <>
        {topbar}
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load users'}
        </p>
      </>
    );
  }

  return (
    <>
      {topbar}

      {!users || users.length === 0 ? (
        <p className="text-ink-muted text-sm py-12 text-center">No users yet.</p>
      ) : (
        <div className="border border-border-strong rounded-md bg-card overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Email</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Role</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Organization</TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Active</TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">Invite Sent</TableHead>
                <TableHead className="w-[50px]"></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.map((user) => (
                <TableRow
                  key={user.id}
                  className="cursor-pointer"
                  onClick={() => void navigate(`/admin/users/${user.id}`)}
                >
                  <TableCell className="font-medium">{[user.firstName, user.lastName].filter(Boolean).join(' ') || user.email}</TableCell>
                  <TableCell>{user.email}</TableCell>
                  <TableCell>{user.role}</TableCell>
                  <TableCell className="hidden md:table-cell">
                    {(user.organizationId && orgMap.get(user.organizationId)) ?? '—'}
                  </TableCell>
                  <TableCell>
                    {user.isActive ? (
                      <Badge>Active</Badge>
                    ) : (
                      <Badge variant="secondary">Inactive</Badge>
                    )}
                  </TableCell>
                  <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                    {user.inviteSentAt
                      ? new Date(user.inviteSentAt).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
                      : '—'}
                  </TableCell>
                  <TableCell className="w-[50px]" onClick={(e) => e.stopPropagation()}>
                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon" className="h-8 w-8">
                          <MoreHorizontal className="h-4 w-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem
                          onClick={() => inviteUser.mutate(user.id)}
                          disabled={inviteUser.isPending}
                        >
                          {user.inviteSentAt ? 'Resend Invite' : 'Send Invite'}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
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
