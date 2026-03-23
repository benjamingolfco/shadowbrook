import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { getCourseToday } from '@/lib/course-time';
import { useCourseContext } from '../context/CourseContext';
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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useCreateWaitlistRequest } from '../hooks/useWaitlist';

const addTeeTimeRequestSchema = z.object({
  teeTime: z.string().min(1, 'Tee time is required'),
  golfersNeeded: z.number().min(1, 'At least 1 golfer needed').max(4, 'Maximum 4 golfers'),
});

type AddTeeTimeRequestFormData = z.infer<typeof addTeeTimeRequestSchema>;

interface AddTeeTimeRequestDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  courseId: string;
}

export function AddTeeTimeRequestDialog({ open, onOpenChange, courseId }: AddTeeTimeRequestDialogProps) {
  const createMutation = useCreateWaitlistRequest();
  const { course } = useCourseContext();
  const todayDate = getCourseToday(course?.timeZoneId ?? 'UTC');

  const form = useForm<AddTeeTimeRequestFormData>({
    resolver: zodResolver(addTeeTimeRequestSchema),
    defaultValues: {
      teeTime: '',
      golfersNeeded: 1,
    },
  });

  function onSubmit(data: AddTeeTimeRequestFormData) {
    createMutation.mutate(
      {
        courseId,
        data: {
          date: todayDate,
          teeTime: data.teeTime,
          golfersNeeded: data.golfersNeeded,
        },
      },
      {
        onSuccess: () => {
          form.reset({ teeTime: '', golfersNeeded: 1 });
          onOpenChange(false);
        },
      },
    );
  }

  function handleOpenChange(nextOpen: boolean) {
    onOpenChange(nextOpen);
    if (!nextOpen) {
      form.reset({ teeTime: '', golfersNeeded: 1 });
      createMutation.reset();
    }
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Tee Time Request</DialogTitle>
          <DialogDescription>
            Add a tee time request to the waitlist for today.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
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
                      <SelectTrigger>
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

            {createMutation.isError && (
              <p className="text-sm text-destructive" role="alert">
                {createMutation.error instanceof Error
                  ? createMutation.error.message
                  : 'Failed to add tee time to waitlist.'}
              </p>
            )}

            <DialogFooter>
              <Button type="submit" disabled={createMutation.isPending}>
                {createMutation.isPending ? 'Adding...' : 'Add Request'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
