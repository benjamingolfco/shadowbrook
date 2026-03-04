import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
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
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { useCourseContext } from '../context/CourseContext';
import { useWaitlist, useCreateWaitlistRequest } from '../hooks/useWaitlist';

const addWaitlistRequestSchema = z.object({
  teeTime: z.string().min(1, 'Tee time is required'),
  golfersNeeded: z.number().min(1, 'At least 1 golfer needed').max(4, 'Maximum 4 golfers'),
});

type AddWaitlistRequestFormData = z.infer<typeof addWaitlistRequestSchema>;

function getTodayDate(): string {
  const today = new Date();
  const isoString = today.toISOString().split('T')[0];
  return isoString ?? '';
}

function formatTime(timeString: string): string {
  const parts = timeString.split(':');
  const hours = parts[0] ?? '0';
  const minutes = parts[1] ?? '00';
  const hour = parseInt(hours, 10);
  const ampm = hour >= 12 ? 'PM' : 'AM';
  const displayHour = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
  return `${displayHour}:${minutes} ${ampm}`;
}

export default function Waitlist() {
  const { course } = useCourseContext();
  const [selectedDate, setSelectedDate] = useState<string>(getTodayDate());

  const waitlistQuery = useWaitlist(course?.id, selectedDate);
  const createMutation = useCreateWaitlistRequest();

  const form = useForm<AddWaitlistRequestFormData>({
    resolver: zodResolver(addWaitlistRequestSchema),
    defaultValues: {
      teeTime: '',
      golfersNeeded: 1,
    },
  });

  if (!course) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <p className="text-muted-foreground">
          Select a course from the sidebar to manage the waitlist.
        </p>
      </div>
    );
  }

  function onSubmit(data: AddWaitlistRequestFormData) {
    if (!course) return;
    createMutation.mutate(
      {
        courseId: course.id,
        data: {
          date: selectedDate,
          teeTime: data.teeTime,
          golfersNeeded: data.golfersNeeded,
        },
      },
      {
        onSuccess: () => {
          form.reset({ teeTime: '', golfersNeeded: 1 });
        },
      },
    );
  }

  const isLoading = waitlistQuery.isLoading;
  const waitlistData = waitlistQuery.data;

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold">Waitlist</h1>
      <p className="text-muted-foreground">Manage walk-up golfers for open tee times</p>

      {isLoading && (
        <div className="mt-6 space-y-4">
          <Skeleton className="h-24 w-full max-w-xs" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      )}

      {!isLoading && (
        <>
          <div className="mt-6">
            <div className="space-y-2">
              <label htmlFor="waitlist-date" className="text-sm font-medium">
                Date
              </label>
              <input
                id="waitlist-date"
                type="date"
                value={selectedDate}
                onChange={(e) => setSelectedDate(e.target.value)}
                className="flex h-9 w-full max-w-xs rounded-md border border-input bg-background px-3 py-1 text-base shadow-sm transition-colors file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 md:text-sm"
              />
            </div>
          </div>

          {waitlistData && (
            <div className="mt-6">
              <Card className="w-fit">
                <CardHeader>
                  <CardTitle className="text-sm font-medium text-muted-foreground">
                    Total Golfers Pending
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-3xl font-bold">{waitlistData.totalGolfersPending}</p>
                </CardContent>
              </Card>
            </div>
          )}

          <div className="mt-6">
            <h2 className="text-lg font-semibold">Add to Waitlist</h2>
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)}>
                <div className="mt-3 flex flex-col gap-4 md:flex-row md:items-end">
                  <FormField
                    control={form.control}
                    name="teeTime"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Tee Time</FormLabel>
                        <FormControl>
                          <Input type="time" {...field} />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="golfersNeeded"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Golfers Needed</FormLabel>
                        <Select
                          value={String(field.value)}
                          onValueChange={(v) => field.onChange(Number(v))}
                        >
                          <FormControl>
                            <SelectTrigger className="w-[120px]">
                              <SelectValue placeholder="Select" />
                            </SelectTrigger>
                          </FormControl>
                          <SelectContent>
                            <SelectItem value="1">1</SelectItem>
                            <SelectItem value="2">2</SelectItem>
                            <SelectItem value="3">3</SelectItem>
                            <SelectItem value="4">4</SelectItem>
                          </SelectContent>
                        </Select>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  <Button type="submit" disabled={createMutation.isPending}>
                    {createMutation.isPending ? 'Adding...' : 'Add to Waitlist'}
                  </Button>
                </div>

                {createMutation.isSuccess && (
                  <p className="mt-3 text-sm text-green-600" role="status">
                    Tee time added to waitlist.
                  </p>
                )}
                {createMutation.isError && (
                  <p className="mt-3 text-sm text-destructive" role="alert">
                    {createMutation.error instanceof Error
                      ? createMutation.error.message
                      : 'Failed to add tee time to waitlist.'}
                  </p>
                )}
              </form>
            </Form>
          </div>

          {waitlistQuery.isError && (
            <p className="mt-6 text-sm text-destructive">
              {waitlistQuery.error instanceof Error
                ? waitlistQuery.error.message
                : 'Failed to load waitlist entries'}
            </p>
          )}

          {waitlistData && waitlistData.requests.length === 0 && (
            <div className="mt-6 text-center text-muted-foreground">
              No waitlist entries for this date.
            </div>
          )}

          {waitlistData && waitlistData.requests.length > 0 && (
            <div className="mt-6">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tee Time</TableHead>
                    <TableHead>Golfers Needed</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {waitlistData.requests.map((request) => (
                    <TableRow key={request.id}>
                      <TableCell className="font-semibold">
                        {formatTime(request.teeTime)}
                      </TableCell>
                      <TableCell>{request.golfersNeeded}</TableCell>
                      <TableCell>
                        <Badge variant="muted">{request.golfersNeeded} pending</Badge>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
