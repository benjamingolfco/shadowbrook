import { useState } from 'react';
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
import { useJoinWaitlist } from '../hooks/useWalkupJoin';
import type { VerifyCodeResponse, JoinWaitlistResponse } from '@/types/waitlist';

const joinSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  phone: z.string().min(10, 'Enter a valid phone number'),
  partySize: z.number().int().min(1).max(4),
});

type JoinFormData = z.infer<typeof joinSchema>;

interface JoinFormProps {
  verifyData: VerifyCodeResponse;
  onJoined: (result: JoinWaitlistResponse) => void;
  onPhoneCapture?: (phone: string) => void;
}

export default function JoinForm({ verifyData, onJoined, onPhoneCapture }: JoinFormProps) {
  const joinMutation = useJoinWaitlist();
  const [selectedPartySize, setSelectedPartySize] = useState(1);

  const form = useForm<JoinFormData>({
    resolver: zodResolver(joinSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      phone: '',
      partySize: 1,
    },
  });

  function onSubmit(data: JoinFormData) {
    onPhoneCapture?.(data.phone);
    joinMutation.mutate(
      {
        courseWaitlistId: verifyData.courseWaitlistId,
        firstName: data.firstName,
        lastName: data.lastName,
        phone: data.phone,
        partySize: data.partySize,
      },
      {
        onSuccess: (result) => {
          onJoined(result);
        },
        onError: (error) => {
          const err = error as Error & { status?: number };
          // 409 duplicate: treat as success — golfer is already on the list
          if (err.status === 409) {
            onJoined({
              entryId: '',
              golferId: '',
              golferName: `${data.firstName} ${data.lastName}`,
              position: 0,
              courseName: verifyData.courseName,
            });
          }
        },
      },
    );
  }

  function getErrorMessage() {
    if (!joinMutation.isError) return null;
    const err = joinMutation.error as Error & { status?: number };
    if (err.status === 409) return null; // handled as success
    return err.message ?? 'Something went wrong. Please try again.';
  }

  const errorMessage = getErrorMessage();

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h2 className="text-xl font-semibold">{verifyData.courseName}</h2>
        <p className="text-sm text-muted-foreground mt-1">Join the walk-up waitlist</p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
          <FormField
            control={form.control}
            name="firstName"
            render={({ field }) => (
              <FormItem>
                <FormLabel>First Name</FormLabel>
                <FormControl>
                  <Input placeholder="John" {...field} />
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
                  <Input placeholder="Smith" {...field} />
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
                    {...field}
                  />
                </FormControl>
                <FormMessage />
              </FormItem>
            )}
          />

          <FormField
            control={form.control}
            name="partySize"
            render={() => (
              <FormItem>
                <FormLabel>Party Size</FormLabel>
                <div className="flex" role="radiogroup" aria-label="Party size">
                  {[1, 2, 3, 4].map((n) => (
                    <button
                      key={n}
                      type="button"
                      role="radio"
                      aria-checked={selectedPartySize === n}
                      aria-label={String(n)}
                      className={[
                        'h-9 w-10 text-sm font-medium border transition-colors duration-100',
                        n === 1 ? 'rounded-l-md' : '',
                        n === 4 ? 'rounded-r-md' : '',
                        n > 1 ? '-ml-px' : '',
                        selectedPartySize === n
                          ? 'bg-primary text-primary-foreground border-primary z-10 relative'
                          : 'bg-background text-foreground border-input hover:bg-muted',
                      ]
                        .filter(Boolean)
                        .join(' ')}
                      onClick={() => {
                        setSelectedPartySize(n);
                        form.setValue('partySize', n);
                      }}
                    >
                      {n}
                    </button>
                  ))}
                </div>
                <FormMessage />
              </FormItem>
            )}
          />

          {errorMessage && (
            <p className="text-sm text-destructive" role="alert">
              {errorMessage}
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
