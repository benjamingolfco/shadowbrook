import { useParams, Link, useNavigate } from 'react-router';
import { useState } from 'react';
import { useUsers, useUpdateUser, useInviteUser, useDeleteUser } from '../hooks/useUsers';
import { useOrganizations } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';

export default function UserDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: users, isLoading, error } = useUsers();
  const { data: orgs, isLoading: isLoadingOrgs } = useOrganizations();
  const updateUser = useUpdateUser();
  const inviteUser = useInviteUser();
  const navigate = useNavigate();
  const deleteUser = useDeleteUser();

  const user = users?.find((u) => u.id === id);

  const [role, setRole] = useState<string | undefined>(undefined);
  const [organizationId, setOrganizationId] = useState<string | undefined>(undefined);

  const effectiveRole = role ?? user?.role ?? '';
  const effectiveOrgId = organizationId ?? user?.organizationId ?? '';

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading user...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load user'}
        </p>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="p-6">
        <p className="text-destructive">User not found.</p>
        <Button variant="outline" size="sm" asChild className="mt-4">
          <Link to="/admin/users">Back to Users</Link>
        </Button>
      </div>
    );
  }

  function handleSave() {
    if (!user) return;
    updateUser.mutate({
      id: user.id,
      role: effectiveRole || undefined,
      organizationId: effectiveOrgId || null,
    });
  }

  function handleToggleActive() {
    if (!user) return;
    updateUser.mutate({
      id: user.id,
      isActive: !user.isActive,
    });
  }

  function handleDelete() {
    if (!user) return;
    deleteUser.mutate(user.id, {
      onSuccess: () => {
        void navigate('/admin/users');
      },
    });
  }

  return (
    <div className="p-6 max-w-2xl space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="outline" size="sm" asChild>
          <Link to="/admin/users">Back</Link>
        </Button>
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">User Detail</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center justify-between">
            <span>{[user.firstName, user.lastName].filter(Boolean).join(' ') || user.email}</span>
            {user.isActive ? (
              <Badge className="bg-green-100 text-green-800 hover:bg-green-100">Active</Badge>
            ) : (
              <Badge variant="secondary">Inactive</Badge>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="grid grid-cols-1 gap-4">
            <div>
              <p className="text-sm font-medium text-muted-foreground">Name</p>
              <p className="mt-1">{[user.firstName, user.lastName].filter(Boolean).join(' ') || '—'}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Email</p>
              <p className="mt-1">{user.email}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Invite Sent</p>
              <p className="mt-1">
                {user.inviteSentAt
                  ? new Date(user.inviteSentAt).toLocaleDateString(undefined, {
                      year: 'numeric',
                      month: 'short',
                      day: 'numeric',
                      hour: 'numeric',
                      minute: '2-digit',
                    })
                  : 'Not sent'}
              </p>
            </div>
          </div>

          <div className="space-y-4">
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="role-select">
                Role
              </label>
              <Select onValueChange={setRole} value={effectiveRole}>
                <SelectTrigger id="role-select">
                  <SelectValue placeholder="Select a role" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Admin">Admin</SelectItem>
                  <SelectItem value="Operator">Operator</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {effectiveRole === 'Operator' && (
              <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="org-select">
                  Organization
                </label>
                <Select
                  onValueChange={setOrganizationId}
                  value={effectiveOrgId}
                  disabled={isLoadingOrgs || !orgs || orgs.length === 0}
                >
                  <SelectTrigger id="org-select">
                    <SelectValue
                      placeholder={
                        isLoadingOrgs
                          ? 'Loading organizations...'
                          : !orgs || orgs.length === 0
                            ? 'No organizations available'
                            : 'Select an organization'
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    {orgs &&
                      orgs
                        .slice()
                        .sort((a, b) => a.name.localeCompare(b.name))
                        .map((org) => (
                          <SelectItem key={org.id} value={org.id}>
                            {org.name}
                          </SelectItem>
                        ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>

          {updateUser.isError && (
            <p className="text-sm text-destructive">
              Error: {updateUser.error instanceof Error ? updateUser.error.message : 'Update failed'}
            </p>
          )}

          <div className="flex gap-4 pt-2">
            <Button onClick={handleSave} disabled={updateUser.isPending}>
              {updateUser.isPending ? 'Saving...' : 'Save Changes'}
            </Button>
            <Button
              variant="outline"
              onClick={handleToggleActive}
              disabled={updateUser.isPending}
            >
              {user.isActive ? 'Deactivate' : 'Activate'}
            </Button>
            <Button
              variant="outline"
              onClick={() => inviteUser.mutate(user.id)}
              disabled={inviteUser.isPending}
            >
              {inviteUser.isPending
                ? 'Sending...'
                : user.inviteSentAt
                  ? 'Resend Invite'
                  : 'Send Invite'}
            </Button>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="destructive" disabled={deleteUser.isPending}>
                  {deleteUser.isPending ? 'Deleting...' : 'Delete User'}
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Delete user?</AlertDialogTitle>
                  <AlertDialogDescription>
                    This will permanently remove the user from Entra ID. This cannot be undone.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction onClick={handleDelete}>Delete</AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
