import { useParams, Link } from 'react-router';
import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useOrganization, useUpdateOrganization } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { DetailTitle } from '../components/DetailTitle';

const schema = z.object({
  name: z.string().min(1, 'Organization name is required'),
});

type FormData = z.infer<typeof schema>;

export default function OrgDetail() {
  const { id } = useParams<{ id: string }>();
  const { data: org, isLoading, error } = useOrganization(id ?? '');
  const updateMutation = useUpdateOrganization();

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { name: '' },
  });

  useEffect(() => {
    if (org) {
      form.reset({ name: org.name });
    }
  }, [org, form]);

  function onSubmit(data: FormData) {
    if (!id) return;
    updateMutation.mutate({ id, ...data });
  }

  return (
    <>
      <PageTopbar middle={<DetailTitle backTo="/admin/organizations" title={org?.name} />} />

      {error && (
        <div className="max-w-4xl mb-4">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Failed to load organization'}
          </p>
        </div>
      )}

      <Tabs defaultValue="details" className="max-w-4xl">
        <TabsList>
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="courses">Courses</TabsTrigger>
          <TabsTrigger value="users">Users</TabsTrigger>
        </TabsList>

        <TabsContent value="details">
          <Card className="border-border-strong">
            <CardHeader>
              <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
                Details
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {isLoading ? (
                <div className="space-y-3">
                  <Skeleton className="h-4 w-32" />
                  <Skeleton className="h-9 w-full" />
                  <Skeleton className="h-4 w-48" />
                </div>
              ) : (
                <Form {...form}>
                  <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                    <FormField
                      control={form.control}
                      name="name"
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Organization Name *</FormLabel>
                          <FormControl>
                            <Input {...field} />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {org && (
                      <p className="text-sm text-ink-muted">
                        Created {new Date(org.createdAt).toLocaleDateString()}
                      </p>
                    )}

                    {updateMutation.isError && (
                      <p className="text-sm text-destructive">
                        {updateMutation.error instanceof Error
                          ? updateMutation.error.message
                          : 'Failed to save changes'}
                      </p>
                    )}

                    {updateMutation.isSuccess && (
                      <p className="text-sm text-green">Changes saved.</p>
                    )}

                    <Button type="submit" disabled={updateMutation.isPending}>
                      {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
                    </Button>
                  </form>
                </Form>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="courses">
          <Card className="border-border-strong">
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
                Courses
              </CardTitle>
              {id && (
                <Button variant="outline" size="sm" asChild>
                  <Link to={`/admin/courses/new?organizationId=${id}`}>Register Course</Link>
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="p-4 space-y-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                </div>
              ) : !org?.courses.length ? (
                <p className="text-sm text-ink-muted p-4">No courses in this organization.</p>
              ) : (
                <div className="border-t border-border-strong overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-canvas">
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {org.courses.map((course) => (
                        <TableRow key={course.id}>
                          <TableCell>
                            <Link
                              to={`/admin/courses/${course.id}`}
                              className="font-medium hover:underline"
                            >
                              {course.name}
                            </Link>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="users">
          <Card className="border-border-strong">
            <CardHeader className="flex flex-row items-center justify-between">
              <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
                Users
              </CardTitle>
              {id && (
                <Button variant="outline" size="sm" asChild>
                  <Link to={`/admin/users/new?organizationId=${id}`}>Create User</Link>
                </Button>
              )}
            </CardHeader>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="p-4 space-y-2">
                  <Skeleton className="h-4 w-full" />
                  <Skeleton className="h-4 w-full" />
                </div>
              ) : !org?.users.length ? (
                <p className="text-sm text-ink-muted p-4">No users in this organization.</p>
              ) : (
                <div className="border-t border-border-strong overflow-hidden">
                  <Table>
                    <TableHeader>
                      <TableRow className="bg-canvas">
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Name</TableHead>
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Email</TableHead>
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Role</TableHead>
                        <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">Status</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {org.users.map((user) => (
                        <TableRow key={user.id}>
                          <TableCell className="font-medium">{[user.firstName, user.lastName].filter(Boolean).join(' ') || user.email}</TableCell>
                          <TableCell className="text-sm text-ink-muted">{user.email}</TableCell>
                          <TableCell className="text-sm">{user.role}</TableCell>
                          <TableCell>
                            <Badge variant={user.isActive ? 'default' : 'secondary'}>
                              {user.isActive ? 'Active' : 'Inactive'}
                            </Badge>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </>
  );
}
