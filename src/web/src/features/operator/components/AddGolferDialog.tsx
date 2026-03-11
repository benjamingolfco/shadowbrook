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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useAddGolferToWaitlist } from '../hooks/useWaitlist';

const addGolferSchema = z.object({
  firstName: z.string().min(1, 'First name is required'),
  lastName: z.string().min(1, 'Last name is required'),
  phone: z.string().min(1, 'Phone number is required'),
  groupSize: z.number().min(1, 'At least 1 golfer').max(4, 'Maximum 4 golfers'),
});

type AddGolferFormData = z.infer<typeof addGolferSchema>;

interface AddGolferDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  courseId: string;
}

export function AddGolferDialog({ open, onOpenChange, courseId }: AddGolferDialogProps) {
  const addMutation = useAddGolferToWaitlist();

  const form = useForm<AddGolferFormData>({
    resolver: zodResolver(addGolferSchema),
    defaultValues: {
      firstName: '',
      lastName: '',
      phone: '',
      groupSize: 1,
    },
  });

  function onSubmit(data: AddGolferFormData) {
    addMutation.mutate(
      { courseId, data },
      {
        onSuccess: () => {
          form.reset();
          onOpenChange(false);
        },
      },
    );
  }

  function handleOpenChange(nextOpen: boolean) {
    onOpenChange(nextOpen);
    if (!nextOpen) {
      form.reset();
      addMutation.reset();
    }
  }

  const is409 =
    addMutation.isError &&
    'status' in addMutation.error &&
    (addMutation.error as { status: number }).status === 409;

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Golfer to Waitlist</DialogTitle>
          <DialogDescription>
            Add a walk-up golfer to the waitlist on their behalf.
          </DialogDescription>
        </DialogHeader>
        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <FormField
                control={form.control}
                name="firstName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>First Name</FormLabel>
                    <FormControl>
                      <Input {...field} />
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
                      <Input {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            <FormField
              control={form.control}
              name="phone"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Phone Number</FormLabel>
                  <FormControl>
                    <Input type="tel" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="groupSize"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Group Size</FormLabel>
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

            {is409 && (
              <p className="text-sm text-destructive" role="alert">
                This golfer is already on the waitlist.
              </p>
            )}

            {addMutation.isError && !is409 && (
              <p className="text-sm text-destructive" role="alert">
                {addMutation.error instanceof Error
                  ? addMutation.error.message
                  : 'Failed to add golfer to waitlist.'}
              </p>
            )}

            <DialogFooter>
              <Button type="submit" disabled={addMutation.isPending}>
                {addMutation.isPending ? 'Adding...' : 'Add Golfer'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
