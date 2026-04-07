import { useNavigate, Link, useSearchParams } from 'react-router';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useCreateUser } from '../hooks/useUsers';
import { useOrganizations } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormMessage,
} from '@/components/ui/form';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { DetailTitle } from '../components/DetailTitle';

const userSchema = z
  .object({
    email: z.string().email('Invalid email address'),
    role: z.enum(['Admin', 'Operator']),
    organizationId: z.string().optional(),
    sendInvite: z.boolean(),
  })
  .check((ctx) => {
    if (ctx.value.role === 'Operator' && !ctx.value.organizationId) {
      ctx.issues.push({
        code: 'custom',
        input: ctx.value,
        message: 'Organization is required for Operator role',
        path: ['organizationId'],
      });
    }
  });

type UserFormData = z.infer<typeof userSchema>;

export default function UserCreate() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const preselectedOrgId = searchParams.get('organizationId') ?? '';
  const { data: orgs, isLoading: isLoadingOrgs } = useOrganizations();
  const createUser = useCreateUser();

  const form = useForm<UserFormData>({
    resolver: zodResolver(userSchema),
    defaultValues: {
      email: '',
      role: 'Operator',
      organizationId: preselectedOrgId,
      sendInvite: false,
    },
  });

  const role = useWatch({ control: form.control, name: 'role' });

  function onSubmit(data: UserFormData) {
    createUser.mutate(
      {
        email: data.email,
        role: data.role,
        organizationId: data.organizationId ?? null,
        sendInvite: data.sendInvite,
      },
      {
        onSuccess: () => {
          void navigate('/admin/users');
        },
      },
    );
  }

  return (
    <>
      <PageTopbar middle={<DetailTitle backTo="/admin/users" title="Create User" />} />

      <div className="max-w-2xl">
      <Card className="border-border-strong">
        <CardHeader>
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            User Details
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
              <FormField
                control={form.control}
                name="email"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email *</FormLabel>
                    <FormControl>
                      <Input type="email" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="role"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Role *</FormLabel>
                    <Select onValueChange={field.onChange} value={field.value}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select a role" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="Admin">Admin</SelectItem>
                        <SelectItem value="Operator">Operator</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {role === 'Operator' && (
                <FormField
                  control={form.control}
                  name="organizationId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Organization *</FormLabel>
                      <Select
                        onValueChange={field.onChange}
                        value={field.value ?? ''}
                        disabled={isLoadingOrgs || !orgs || orgs.length === 0}
                      >
                        <FormControl>
                          <SelectTrigger>
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
                        </FormControl>
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
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              <FormField
                control={form.control}
                name="sendInvite"
                render={({ field }) => (
                  <FormItem className="flex items-center gap-3 space-y-0">
                    <FormControl>
                      <Checkbox
                        checked={field.value}
                        onCheckedChange={field.onChange}
                      />
                    </FormControl>
                    <FormLabel className="font-normal">
                      Send Invite
                    </FormLabel>
                  </FormItem>
                )}
              />

              {createUser.isError && (
                <p className="text-sm text-destructive">
                  Error: {createUser.error instanceof Error ? createUser.error.message : 'Failed to create user'}
                </p>
              )}

              <div className="flex gap-3">
                <Button type="submit" disabled={createUser.isPending}>
                  {createUser.isPending ? 'Creating...' : 'Create User'}
                </Button>
                <Button type="button" variant="outline" asChild>
                  <Link to="/admin/users">Cancel</Link>
                </Button>
              </div>
            </form>
          </Form>
        </CardContent>
      </Card>
      </div>
    </>
  );
}
