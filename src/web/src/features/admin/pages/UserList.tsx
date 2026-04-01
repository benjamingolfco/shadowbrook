import { Link, useNavigate } from 'react-router';
import { useUsers } from '../hooks/useUsers';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';

export default function UserList() {
  const { data: users, isLoading, error } = useUsers();
  const navigate = useNavigate();

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading users...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load users'}
        </p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Users</h1>
          <p className="text-sm text-muted-foreground">Platform Admin View</p>
        </div>
        <Button asChild>
          <Link to="/admin/users/new">Create User</Link>
        </Button>
      </div>

      {!users || users.length === 0 ? (
        <p className="text-muted-foreground">No users yet.</p>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Display Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Role</TableHead>
                <TableHead className="hidden md:table-cell">Organization</TableHead>
                <TableHead>Active</TableHead>
                <TableHead className="hidden md:table-cell">Created</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {users.map((user) => (
                <TableRow
                  key={user.id}
                  className="cursor-pointer"
                  onClick={() => void navigate(`/admin/users/${user.id}`)}
                >
                  <TableCell className="font-medium">{user.displayName}</TableCell>
                  <TableCell>{user.email}</TableCell>
                  <TableCell>{user.role}</TableCell>
                  <TableCell className="hidden md:table-cell">
                    {user.organizationId ?? '—'}
                  </TableCell>
                  <TableCell>
                    {user.isActive ? (
                      <Badge className="bg-green-100 text-green-800 hover:bg-green-100">Active</Badge>
                    ) : (
                      <Badge variant="secondary">Inactive</Badge>
                    )}
                  </TableCell>
                  <TableCell className="hidden md:table-cell text-sm text-muted-foreground">
                    —
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
