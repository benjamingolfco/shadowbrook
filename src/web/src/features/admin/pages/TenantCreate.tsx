import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { useNavigate } from 'react-router';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Tenant } from '@/types/tenant';
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

const tenantSchema = z.object({
  organizationName: z.string().min(1, 'Organization name is required'),
  contactName: z.string().min(1, 'Contact name is required'),
  contactEmail: z.string().min(1, 'Contact email is required').email('Invalid email address'),
  contactPhone: z.string().min(1, 'Contact phone is required'),
});

type TenantFormData = z.infer<typeof tenantSchema>;

export default function TenantCreate() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const form = useForm<TenantFormData>({
    resolver: zodResolver(tenantSchema),
    defaultValues: {
      organizationName: '',
      contactName: '',
      contactEmail: '',
      contactPhone: '',
    },
  });

  const createTenantMutation = useMutation({
    mutationFn: (data: TenantFormData) => api.post<Tenant>('/tenants', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.tenants.all });
      form.reset();
      navigate('/admin/tenants');
    },
  });

  function onSubmit(data: TenantFormData) {
    createTenantMutation.mutate(data);
  }

  return (
    <div className="p-6 max-w-2xl">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Register a Tenant</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Create a new tenant organization that can manage multiple golf courses
        </p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
          <FormField
            control={form.control}
            name="organizationName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Organization Name *</FormLabel>
                <FormControl>
                  <Input {...field} placeholder="e.g., Pinecrest Golf Management" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="contactName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Contact Name *</FormLabel>
                <FormControl>
                  <Input {...field} placeholder="e.g., John Smith" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="contactEmail"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Contact Email *</FormLabel>
                <FormControl>
                  <Input type="email" {...field} placeholder="e.g., john@pinecrest.com" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="contactPhone"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Contact Phone *</FormLabel>
                <FormControl>
                  <Input type="tel" {...field} placeholder="e.g., 555-1234" />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          {createTenantMutation.isError && (
            <div className="text-destructive text-sm">
              Error: {createTenantMutation.error.message}
            </div>
          )}

          <div className="flex gap-4">
            <Button type="submit" disabled={createTenantMutation.isPending}>
              {createTenantMutation.isPending ? 'Registering...' : 'Register Tenant'}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate('/admin/tenants')}
            >
              Cancel
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}
