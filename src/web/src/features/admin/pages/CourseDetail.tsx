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
      <div className="p-6">
        <p className="text-muted-foreground">Loading course...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load courses'}
        </p>
      </div>
    );
  }

  if (!course) {
    return (
      <div className="p-6 space-y-4">
        <Button variant="ghost" asChild>
          <Link to="/admin/courses">&larr; Back to Courses</Link>
        </Button>
        <p className="text-muted-foreground">Course not found.</p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" asChild>
          <Link to="/admin/courses">&larr; Back to Courses</Link>
        </Button>
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
          {course.name}
        </h1>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Course Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div>
              <p className="text-sm font-medium text-muted-foreground">Organization</p>
              <p className="text-sm">{course.tenantName ?? '—'}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Timezone</p>
              <p className="text-sm">{course.timeZoneId}</p>
            </div>
            {(course.streetAddress ?? course.city ?? course.state ?? course.zipCode) && (
              <div>
                <p className="text-sm font-medium text-muted-foreground">Address</p>
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
                <p className="text-sm font-medium text-muted-foreground">Contact</p>
                {course.contactEmail && (
                  <p className="text-sm">{course.contactEmail}</p>
                )}
                {course.contactPhone && (
                  <p className="text-sm">{course.contactPhone}</p>
                )}
              </div>
            )}
            <div>
              <p className="text-sm font-medium text-muted-foreground">Registered</p>
              <p className="text-sm">{new Date(course.createdAt).toLocaleDateString()}</p>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Edit Course</CardTitle>
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
                  <p className="text-sm text-green-600">Changes saved.</p>
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
    </div>
  );
}
