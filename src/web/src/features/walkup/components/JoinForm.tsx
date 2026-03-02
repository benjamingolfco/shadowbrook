import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
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
import { useJoinWaitlist } from '../hooks/useJoinWaitlist';

const joinFormSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  phone: z
    .string()
    .min(1, 'Phone number is required')
    .regex(/^[\d\s\-\(\)\+]+$/, 'Please enter a valid phone number')
    .refine(
      (val) => val.replace(/\D/g, '').length >= 10,
      'Phone number must be at least 10 digits',
    ),
});

type JoinFormData = z.infer<typeof joinFormSchema>;

interface JoinFormProps {
  courseWaitlistId: string;
  courseName: string;
  onJoined: (data: { firstName: string; position: number; isExisting: boolean }) => void;
}

export default function JoinForm({ courseWaitlistId, courseName, onJoined }: JoinFormProps) {
  const joinMutation = useJoinWaitlist();

  const form = useForm<JoinFormData>({
    resolver: zodResolver(joinFormSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      phone: '',
    },
  });

  function onSubmit(data: JoinFormData) {
    joinMutation.mutate(
      {
        courseWaitlistId,
        firstName: data.firstName,
        lastName: data.lastName,
        phone: data.phone,
      },
      {
        onSuccess: (response) => {
          onJoined({
            firstName: response.firstName,
            position: response.position,
            isExisting: response.isExisting,
          });
        },
      },
    );
  }

  return (
    <div className="w-full max-w-xs">
      <h2 className="text-xl font-semibold text-gray-900 mb-6 text-center">{courseName}</h2>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4" noValidate>
          <FormField
            control={form.control}
            name="firstName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>First Name</FormLabel>
                <FormControl>
                  <Input
                    placeholder="Tiger"
                    className="h-12"
                    autoComplete="given-name"
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="lastName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Last Name</FormLabel>
                <FormControl>
                  <Input
                    placeholder="Woods"
                    className="h-12"
                    autoComplete="family-name"
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="phone"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Phone Number</FormLabel>
                <FormControl>
                  <Input
                    type="tel"
                    inputMode="tel"
                    placeholder="(555) 123-4567"
                    className="h-12"
                    autoComplete="tel"
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          {joinMutation.isError && (
            <p className="text-sm text-destructive" role="alert">
              {joinMutation.error.message}
            </p>
          )}

          <Button
            type="submit"
            size="lg"
            className="w-full"
            disabled={joinMutation.isPending}
          >
            {joinMutation.isPending ? 'Joining...' : 'Join Waitlist'}
          </Button>
        </form>
      </Form>
    </div>
  );
}
