import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { z } from 'zod';
import { useNavigate, Link } from 'react-router';
import { api } from '@/lib/api-client';
import type { Course } from '@/types/course';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useTenants } from '../hooks/useTenants';

const courseSchema = z.object({
  tenantId: z.string().min(1, 'Tenant assignment is required'),
  name: z.string().min(1, 'Course name is required'),
  streetAddress: z.string().optional(),
  city: z.string().optional(),
  state: z.string().optional(),
  zipCode: z.string().optional(),
  contactEmail: z.union([z.string().email('Invalid email address'), z.literal('')]).optional(),
  contactPhone: z.string().optional(),
});

type CourseFormData = z.infer<typeof courseSchema>;

export default function CourseCreate() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const { data: tenants, isLoading: isLoadingTenants, error: tenantsError } = useTenants();

  const form = useForm<CourseFormData>({
    resolver: zodResolver(courseSchema),
    defaultValues: {
      tenantId: '',
      name: '',
      streetAddress: '',
      city: '',
      state: '',
      zipCode: '',
      contactEmail: '',
      contactPhone: '',
    },
  });

  const createCourseMutation = useMutation({
    mutationFn: (data: CourseFormData) => api.post<Course>('/courses', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['courses'] });
      form.reset();
      navigate('/admin/courses');
    },
  });

  function onSubmit(data: CourseFormData) {
    createCourseMutation.mutate(data);
  }

  return (
    <div className="p-6 max-w-2xl">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Register a Course</h1>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
          <FormField
            control={form.control}
            name="tenantId"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Assign to Tenant *</FormLabel>
                <Select onValueChange={field.onChange} value={field.value} disabled={isLoadingTenants || !tenants || tenants.length === 0}>
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder={
                        isLoadingTenants ? 'Loading tenants...' :
                        !tenants || tenants.length === 0 ? 'No tenants available' :
                        'Select a tenant'
                      } />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    {tenants && tenants
                      .sort((a, b) => a.organizationName.localeCompare(b.organizationName))
                      .map((tenant) => (
                        <SelectItem key={tenant.id} value={tenant.id}>
                          {tenant.organizationName}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
                {tenantsError && (
                  <p className="text-sm text-destructive">
                    Error loading tenants: {tenantsError instanceof Error ? tenantsError.message : 'Unknown error'}
                  </p>
                )}
                {!isLoadingTenants && tenants && tenants.length === 0 && (
                  <p className="text-sm text-muted-foreground">
                    No tenants found.{' '}
                    <Link to="/admin/tenants/new" className="text-primary underline">
                      Create a tenant
                    </Link>{' '}
                    first.
                  </p>
                )}
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="name"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Course Name *</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="streetAddress"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Street Address</FormLabel>
                <FormControl>
                  <Input {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <FormField
              control={form.control}
              name="city"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>City</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="state"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>State</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="zipCode"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Zip Code</FormLabel>
                  <FormControl>
                    <Input {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="contactEmail"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Contact Email</FormLabel>
                <FormControl>
                  <Input type="email" {...field} />
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
                <FormLabel>Contact Phone</FormLabel>
                <FormControl>
                  <Input type="tel" {...field} />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          {createCourseMutation.isError && (
            <div className="text-destructive text-sm">
              Error: {createCourseMutation.error.message}
            </div>
          )}

          <div className="flex gap-4">
            <Button type="submit" disabled={createCourseMutation.isPending}>
              {createCourseMutation.isPending ? 'Registering...' : 'Register Course'}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => navigate('/admin/courses')}
            >
              Cancel
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}
