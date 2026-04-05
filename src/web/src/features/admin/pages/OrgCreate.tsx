import { useNavigate, Link } from 'react-router';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useCreateOrganization } from '../hooks/useOrganizations';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
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

const schema = z.object({
  name: z.string().min(1, 'Organization name is required'),
  operatorEmail: z.string().email('Invalid email address'),
  sendInvite: z.boolean(),
});

type FormData = z.infer<typeof schema>;

export default function OrgCreate() {
  const navigate = useNavigate();
  const createMutation = useCreateOrganization();

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', operatorEmail: '', sendInvite: false },
  });

  function onSubmit(data: FormData) {
    createMutation.mutate(data, {
      onSuccess: () => {
        navigate('/admin/organizations');
      },
    });
  }

  return (
    <div className="p-6 max-w-lg">
      <div className="mb-6 flex items-center gap-4">
        <Button variant="outline" size="sm" asChild>
          <Link to="/admin/organizations">Back</Link>
        </Button>
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">Create Organization</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Organization Details</CardTitle>
        </CardHeader>
        <CardContent>
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
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

              <FormField
                control={form.control}
                name="operatorEmail"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>First Operator Email *</FormLabel>
                    <FormControl>
                      <Input type="email" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

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
                      Send Invite to First Operator
                    </FormLabel>
                  </FormItem>
                )}
              />

              {createMutation.isError && (
                <p className="text-sm text-destructive">
                  {createMutation.error instanceof Error
                    ? createMutation.error.message
                    : 'Failed to create organization'}
                </p>
              )}

              <div className="flex gap-4">
                <Button type="submit" disabled={createMutation.isPending}>
                  {createMutation.isPending ? 'Creating...' : 'Create Organization'}
                </Button>
                <Button type="button" variant="outline" asChild>
                  <Link to="/admin/organizations">Cancel</Link>
                </Button>
              </div>
            </form>
          </Form>
        </CardContent>
      </Card>
    </div>
  );
}
