import { useParams, Link } from 'react-router';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useCourses } from '../hooks/useCourses';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import type { Course } from '@/types/course';

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  timeZoneId: z.string().min(1, 'Timezone is required'),
});

type FormValues = z.infer<typeof schema>;

export default function CourseDetail() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const { data: courses, isLoading, error } = useCourses();

  const course = courses?.find((c: Course) => c.id === id);

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    values: course
      ? { name: course.name, timeZoneId: course.timeZoneId }
      : { name: '', timeZoneId: '' },
  });

  const mutation = useMutation({
    mutationFn: (data: FormValues) =>
      api.put<Course>(`/courses/${id ?? ''}`, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.all });
    },
  });

  function onSubmit(data: FormValues) {
    mutation.mutate(data);
  }

  if (isLoading) {
    return (
      <>
        <PageTopbar middle={<DetailTitle backTo="/admin/courses" />} />
        <p className="text-ink-muted">Loading course...</p>
      </>
    );
  }

  if (error) {
    return (
      <>
        <PageTopbar middle={<DetailTitle backTo="/admin/courses" />} />
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load courses'}
        </p>
      </>
    );
  }

  if (!course) {
    return (
      <>
        <PageTopbar middle={<DetailTitle backTo="/admin/courses" title="Not found" />} />
        <p className="text-ink-muted">Course not found.</p>
        <Button variant="outline" asChild className="mt-4">
          <Link to="/admin/courses">Back to Courses</Link>
        </Button>
      </>
    );
  }

  return (
    <>
      <PageTopbar middle={<DetailTitle backTo="/admin/courses" title={course.name} />} />

      <div className="grid gap-6 md:grid-cols-2 max-w-4xl">
        <Card className="border-border-strong">
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Course Information
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div>
              <p className="text-sm font-medium text-ink-muted">Organization</p>
              <p className="text-sm">{course.tenantName ?? '—'}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-ink-muted">Timezone</p>
              <p className="text-sm">{course.timeZoneId}</p>
            </div>
            {(course.streetAddress ?? course.city ?? course.state ?? course.zipCode) && (
              <div>
                <p className="text-sm font-medium text-ink-muted">Address</p>
                {course.streetAddress && (
                  <p className="text-sm">{course.streetAddress}</p>
                )}
                {(course.city ?? course.state ?? course.zipCode) && (
                  <p className="text-sm">
                    {course.city}
                    {course.city && course.state ? ', ' : ''}
                    {course.state} {course.zipCode}
                  </p>
                )}
              </div>
            )}
            {(course.contactEmail ?? course.contactPhone) && (
              <div>
                <p className="text-sm font-medium text-ink-muted">Contact</p>
                {course.contactEmail && (
                  <p className="text-sm">{course.contactEmail}</p>
                )}
                {course.contactPhone && (
                  <p className="text-sm">{course.contactPhone}</p>
                )}
              </div>
            )}
            <div>
              <p className="text-sm font-medium text-ink-muted">Registered</p>
              <p className="text-sm">{new Date(course.createdAt).toLocaleDateString()}</p>
            </div>
          </CardContent>
        </Card>

        <Card className="border-border-strong">
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Edit Course
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
                <FormField
                  control={form.control}
                  name="name"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Name</FormLabel>
                      <FormControl>
                        <Input {...field} placeholder="Course name" />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="timeZoneId"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Timezone</FormLabel>
                      <FormControl>
                        <Input {...field} placeholder="e.g. America/New_York" />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                {mutation.isSuccess && (
                  <p className="text-sm text-green">Changes saved.</p>
                )}
                {mutation.isError && (
                  <p className="text-sm text-destructive">
                    {mutation.error instanceof Error
                      ? mutation.error.message
                      : 'Failed to save changes'}
                  </p>
                )}
                <Button type="submit" disabled={mutation.isPending}>
                  {mutation.isPending ? 'Saving...' : 'Save Changes'}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
