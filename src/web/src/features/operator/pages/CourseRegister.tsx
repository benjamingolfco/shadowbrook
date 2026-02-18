import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { useNavigate } from 'react-router';
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
import { useRegisterCourse } from '../hooks/useCourseRegister';

const courseRegisterSchema = z.object({
  name: z.string().min(1, 'Course name is required'),
  streetAddress: z.string().optional(),
  city: z.string().optional(),
  state: z.string().optional(),
  zipCode: z.string().optional(),
  contactEmail: z.union([z.string().email('Invalid email address'), z.literal('')]).optional(),
  contactPhone: z.string().optional(),
});

type CourseRegisterFormData = z.infer<typeof courseRegisterSchema>;

export default function CourseRegister() {
  const navigate = useNavigate();
  const registerMutation = useRegisterCourse();

  const form = useForm<CourseRegisterFormData>({
    resolver: zodResolver(courseRegisterSchema),
    defaultValues: {
      name: '',
      streetAddress: '',
      city: '',
      state: '',
      zipCode: '',
      contactEmail: '',
      contactPhone: '',
    },
  });

  function onSubmit(data: CourseRegisterFormData) {
    // Filter out empty optional fields
    const payload = Object.fromEntries(
      Object.entries(data).filter(([, value]) => value !== '')
    ) as CourseRegisterFormData;

    registerMutation.mutate(payload, {
      onSuccess: () => {
        navigate('/operator/settings');
      },
    });
  }

  function handleCancel() {
    navigate('/operator/tee-sheet');
  }

  return (
    <div className="p-6">
      <div className="max-w-2xl mx-auto">
        <div className="mb-6">
          <h1 className="text-2xl font-bold">Register Course</h1>
          <p className="text-muted-foreground mt-1">
            Add your golf course to get started with Shadowbrook
          </p>
        </div>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Course Name *</FormLabel>
                  <FormControl>
                    <Input placeholder="Pine Valley Golf Club" {...field} />
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
                    <Input placeholder="123 Golf Course Drive" {...field} />
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
                      <Input placeholder="Augusta" {...field} />
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
                      <Input placeholder="GA" {...field} />
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
                      <Input placeholder="30901" {...field} />
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
                    <Input type="email" placeholder="contact@golfclub.com" {...field} />
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
                    <Input type="tel" placeholder="(555) 123-4567" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {registerMutation.isError && (
              <div className="text-destructive text-sm">
                {registerMutation.error.message}
              </div>
            )}

            {registerMutation.isSuccess && (
              <div className="text-green-600 text-sm">
                Course registered successfully! Redirecting...
              </div>
            )}

            <div className="flex flex-col sm:flex-row gap-3">
              <Button type="submit" disabled={registerMutation.isPending}>
                {registerMutation.isPending ? 'Registering...' : 'Register Course'}
              </Button>
              <Button
                type="button"
                variant="outline"
                onClick={handleCancel}
                disabled={registerMutation.isPending}
              >
                Cancel
              </Button>
            </div>
          </form>
        </Form>
      </div>
    </div>
  );
}
