import { useEffect } from 'react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
  const { course, registerDirtyForm, unregisterDirtyForm } = useCourseContext();

  const form = useForm<TeeTimeSettingsFormData>({
    resolver: zodResolver(teeTimeSettingsSchema),
    defaultValues: {
      teeTimeIntervalMinutes: 10,
      firstTeeTime: '07:00',
      lastTeeTime: '18:00',
    },
  });

  const settingsQuery = useTeeTimeSettings(course?.id);
  const updateMutation = useUpdateTeeTimeSettings();

  const formIsDirty = form.formState.isDirty;

  useEffect(() => {
    if (formIsDirty) {
      registerDirtyForm('tee-time-settings');
    } else {
      unregisterDirtyForm('tee-time-settings');
    }
    return () => {
      unregisterDirtyForm('tee-time-settings');
    };
  }, [formIsDirty, registerDirtyForm, unregisterDirtyForm]);

  useEffect(() => {
    if (settingsQuery.data?.firstTeeTime && settingsQuery.data.lastTeeTime) {
      form.reset({
        teeTimeIntervalMinutes: settingsQuery.data.teeTimeIntervalMinutes,
        firstTeeTime: settingsQuery.data.firstTeeTime.slice(0, 5),
        lastTeeTime: settingsQuery.data.lastTeeTime.slice(0, 5),
      });
    }
  }, [settingsQuery.data, form]);

  if (!course) return null;

  const courseId = course.id;

  function onSubmit(data: TeeTimeSettingsFormData) {
    updateMutation.mutate({ courseId, data });
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Tee Time Settings</h1>}
      />

      <div className="max-w-2xl">
        <Card className="border-border-strong">
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Tee Time Configuration
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                {settingsQuery.isLoading && (
                  <p className="text-ink-muted text-sm">Loading settings…</p>
                )}
                {settingsQuery.isError && (
                  <p className="text-destructive text-sm">
                    Error loading settings: {settingsQuery.error.message}
                  </p>
                )}

                <FormField
                  control={form.control}
                  name="teeTimeIntervalMinutes"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Tee Time Interval</FormLabel>
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
                        <FormLabel>First Tee Time</FormLabel>
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
                        <FormLabel>Last Tee Time</FormLabel>
                        <FormControl>
                          <Input type="time" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>

                {updateMutation.isError && (
                  <p className="text-destructive text-sm">
                    Error: {updateMutation.error.message}
                  </p>
                )}

                {updateMutation.isSuccess && (
                  <p className="text-green text-sm">
                    Tee time settings saved successfully!
                  </p>
                )}

                <Button type="submit" disabled={updateMutation.isPending}>
                  {updateMutation.isPending ? 'Saving…' : 'Save Settings'}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
