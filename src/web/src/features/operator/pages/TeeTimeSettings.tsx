import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
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
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  useTeeTimeSettings,
  useUpdateTeeTimeSettings,
} from '../hooks/useTeeTimeSettings';
import { useCourseContext } from '../context/CourseContext';

const teeTimeSettingsSchema = z.object({
  teeTimeIntervalMinutes: z.number().refine((val) => [8, 10, 12].includes(val), {
    message: 'Interval must be 8, 10, or 12 minutes',
  }),
  firstTeeTime: z.string().min(1, 'First tee time is required'),
  lastTeeTime: z.string().min(1, 'Last tee time is required'),
});

type TeeTimeSettingsFormData = z.infer<typeof teeTimeSettingsSchema>;

export default function TeeTimeSettings() {
  const { course } = useCourseContext();

  // course is guaranteed non-null by CourseGate in index.tsx
  const settingsQuery = useTeeTimeSettings(course!.id);
  const updateMutation = useUpdateTeeTimeSettings();

  const form = useForm<TeeTimeSettingsFormData>({
    resolver: zodResolver(teeTimeSettingsSchema),
    defaultValues: {
      teeTimeIntervalMinutes: 10,
      firstTeeTime: '07:00',
      lastTeeTime: '18:00',
    },
  });

  // When settings load for the selected course, reset the form
  useEffect(() => {
    if (settingsQuery.data?.firstTeeTime && settingsQuery.data.lastTeeTime) {
      form.reset({
        teeTimeIntervalMinutes: settingsQuery.data.teeTimeIntervalMinutes,
        firstTeeTime: settingsQuery.data.firstTeeTime.slice(0, 5),
        lastTeeTime: settingsQuery.data.lastTeeTime.slice(0, 5),
      });
    }
  }, [settingsQuery.data, form]);

  function onSubmit(data: TeeTimeSettingsFormData) {
    updateMutation.mutate(
      { courseId: course!.id, data },
      {
        onSuccess: () => {
          // Success feedback handled by mutation state
        },
      }
    );
  }

  return (
    <div className="p-6 max-w-2xl">
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Tee Time Settings</h1>
      </div>

      {settingsQuery.isLoading && (
        <p className="text-muted-foreground">Loading settings...</p>
      )}

      {settingsQuery.isError && (
        <div className="text-destructive text-sm mb-4">
          Error loading settings: {settingsQuery.error.message}
        </div>
      )}

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
          <FormField
            control={form.control}
            name="teeTimeIntervalMinutes"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Tee Time Interval *</FormLabel>
                <Select
                  value={String(field.value)}
                  onValueChange={(value) => field.onChange(Number(value))}
                >
                  <FormControl>
                    <SelectTrigger>
                      <SelectValue placeholder="Select interval" />
                    </SelectTrigger>
                  </FormControl>
                  <SelectContent>
                    <SelectItem value="8">Every 8 minutes</SelectItem>
                    <SelectItem value="10">Every 10 minutes</SelectItem>
                    <SelectItem value="12">Every 12 minutes</SelectItem>
                  </SelectContent>
                </Select>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <FormField
              control={form.control}
              name="firstTeeTime"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>First Tee Time *</FormLabel>
                  <FormControl>
                    <Input type="time" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="lastTeeTime"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Last Tee Time *</FormLabel>
                  <FormControl>
                    <Input type="time" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          {updateMutation.isError && (
            <div className="text-destructive text-sm">
              Error: {updateMutation.error.message}
            </div>
          )}

          {updateMutation.isSuccess && (
            <div className="text-green-600 text-sm">
              Tee time settings saved successfully!
            </div>
          )}

          <Button type="submit" disabled={updateMutation.isPending}>
            {updateMutation.isPending ? 'Saving...' : 'Save Settings'}
          </Button>
        </form>
      </Form>
    </div>
  );
}
