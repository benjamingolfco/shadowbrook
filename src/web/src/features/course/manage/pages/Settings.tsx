import { useEffect } from 'react';
import { useBlocker } from 'react-router';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { WarningAlert } from '@/components/ui/warning-alert';
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
import { useCourseId } from '../../hooks/useCourseId';

const teeTimeSettingsSchema = z.object({
  teeTimeIntervalMinutes: z.number().int().min(1, 'Interval must be at least 1 minute'),
  firstTeeTime: z.string().min(1, 'First tee time is required'),
  lastTeeTime: z.string().min(1, 'Last tee time is required'),
  defaultCapacity: z.number().int().min(1, 'Default group size must be at least 1'),
});

type TeeTimeSettingsFormData = z.infer<typeof teeTimeSettingsSchema>;

export default function Settings() {
  const courseId = useCourseId();

  const form = useForm<TeeTimeSettingsFormData>({
    resolver: zodResolver(teeTimeSettingsSchema),
    defaultValues: {
      teeTimeIntervalMinutes: 10,
      firstTeeTime: '07:00',
      lastTeeTime: '18:00',
      defaultCapacity: 4,
    },
  });

  const isDirty = form.formState.isDirty;

  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    isDirty && currentLocation.pathname !== nextLocation.pathname,
  );

  const settingsQuery = useTeeTimeSettings(courseId);
  const updateMutation = useUpdateTeeTimeSettings();

  const intervalValue = form.watch('teeTimeIntervalMinutes');

  useEffect(() => {
    if (settingsQuery.data?.firstTeeTime && settingsQuery.data.lastTeeTime) {
      form.reset({
        teeTimeIntervalMinutes: settingsQuery.data.teeTimeIntervalMinutes,
        firstTeeTime: settingsQuery.data.firstTeeTime.slice(0, 5),
        lastTeeTime: settingsQuery.data.lastTeeTime.slice(0, 5),
        defaultCapacity: settingsQuery.data.defaultCapacity,
      });
    }
  }, [settingsQuery.data, form]);

  function onSubmit(data: TeeTimeSettingsFormData) {
    updateMutation.mutate({ courseId, data });
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Tee Time Settings</h1>}
      />

      {isDirty && (
        <div className="mb-4 rounded-md border border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200">
          You have unsaved changes.
        </div>
      )}

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
                  <p className="text-ink-muted text-sm">Loading settings...</p>
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
                      <FormLabel>Tee Time Interval (minutes)</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={1}
                          {...field}
                          onChange={(e) => field.onChange(e.target.valueAsNumber)}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                {intervalValue > 0 && intervalValue < 8 && (
                  <WarningAlert>
                    Most courses use intervals of 8 minutes or more. Short intervals may cause pace-of-play issues.
                  </WarningAlert>
                )}

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

                <FormField
                  control={form.control}
                  name="defaultCapacity"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Default Group Size</FormLabel>
                      <FormControl>
                        <Input
                          type="number"
                          min={1}
                          {...field}
                          onChange={(e) => field.onChange(e.target.valueAsNumber)}
                        />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />

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
                  {updateMutation.isPending ? 'Saving...' : 'Save Settings'}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>

      <AlertDialog open={blocker.state === 'blocked'}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Unsaved changes</AlertDialogTitle>
            <AlertDialogDescription>
              You have unsaved changes. Are you sure you want to leave this page?
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => blocker.reset?.()}>Stay on page</AlertDialogCancel>
            <AlertDialogAction onClick={() => blocker.proceed?.()}>Leave page</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
