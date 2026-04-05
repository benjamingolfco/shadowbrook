import { useRef, useCallback, useState, useMemo } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { getNextTeeTimeInterval, buildTeeTimeDateTime, getCourseNow } from '@/lib/course-time';
import { useCourseContext } from '../context/CourseContext';
import { useCreateTeeTimeOpening } from '../hooks/useWaitlist';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { ApiError } from '@/lib/api-client';
import type { DuplicateOpeningError } from '@/types/waitlist';

function createSchema(timeZoneId: string) {
  return z
    .object({
      teeTime: z.string().min(1, 'Time is required'),
      slotsAvailable: z.number().min(1).max(4),
    })
    .refine((data) => data.teeTime >= getCourseNow(timeZoneId), {
      message: 'Tee time must be in the future',
      path: ['teeTime'],
    });
}

type FormData = z.infer<ReturnType<typeof createSchema>>;

interface PostTeeTimeFormProps {
  courseId: string;
}

export function PostTeeTimeForm({ courseId }: PostTeeTimeFormProps) {
  const { course } = useCourseContext();
  const timeZoneId = course?.timeZoneId ?? 'UTC';
  const createMutation = useCreateTeeTimeOpening();
  const timeInputRef = useRef<HTMLInputElement | null>(null);
  const [selectedSlots, setSelectedSlots] = useState(1);
  const [showSuccess, setShowSuccess] = useState(false);

  const schema = useMemo(() => createSchema(timeZoneId), [timeZoneId]);

  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      teeTime: getNextTeeTimeInterval(timeZoneId),
      slotsAvailable: 1,
    },
  });

  const { ref: rhfRef, ...teeTimeRegister } = form.register('teeTime');
  const setTimeInputRef = useCallback(
    (el: HTMLInputElement | null) => {
      rhfRef(el);
      timeInputRef.current = el;
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  );

  function onSubmit(data: FormData) {
    createMutation.mutate(
      {
        courseId,
        data: {
          teeTime: buildTeeTimeDateTime(data.teeTime, timeZoneId),
          slotsAvailable: data.slotsAvailable,
        },
      },
      {
        onSuccess: () => {
          form.reset({
            teeTime: getNextTeeTimeInterval(timeZoneId),
            slotsAvailable: 1,
          });
          setSelectedSlots(1);
          createMutation.reset();
          timeInputRef.current?.focus();
          setShowSuccess(true);
          setTimeout(() => setShowSuccess(false), 1500);
        },
      },
    );
  }

  return (
    <Card className="shadow-sm border-border/80">
      <CardContent className="pt-5 pb-5">
        <p className="text-base font-semibold mb-4">Post Tee Time</p>
        <form
          onSubmit={form.handleSubmit(onSubmit)}
          className="flex flex-wrap items-end gap-3"
        >
          <div className="space-y-1">
            <Label htmlFor="tee-time-input" className="text-xs text-muted-foreground">
              Time
            </Label>
            <Input
              id="tee-time-input"
              type="time"
              className="w-[140px]"
              autoFocus
              {...teeTimeRegister}
              ref={setTimeInputRef}
            />
            {form.formState.errors.teeTime && (
              <p className="text-xs text-destructive">
                {form.formState.errors.teeTime.message}
              </p>
            )}
          </div>

          <div className="space-y-1 shrink-0">
            <Label className="text-xs text-muted-foreground">Slots</Label>
            <div className="flex" role="radiogroup" aria-label="Slots">
              {[1, 2, 3, 4].map((n) => (
                <button
                  key={n}
                  type="button"
                  role="radio"
                  aria-checked={selectedSlots === n}
                  aria-label={String(n)}
                  className={[
                    'h-9 w-10 text-sm font-medium border transition-colors duration-100',
                    n === 1 ? 'rounded-l-lg' : '',
                    n === 4 ? 'rounded-r-lg' : '',
                    n > 1 ? '-ml-px' : '',
                    selectedSlots === n
                      ? 'bg-primary text-primary-foreground border-primary z-10 relative'
                      : 'bg-background text-foreground border-input hover:bg-muted',
                  ]
                    .filter(Boolean)
                    .join(' ')}
                  onClick={() => {
                    setSelectedSlots(n);
                    form.setValue('slotsAvailable', n);
                  }}
                >
                  {n}
                </button>
              ))}
            </div>
          </div>

          <Button type="submit" disabled={createMutation.isPending} className="basis-full sm:basis-auto w-full sm:w-auto">
            {showSuccess ? 'Posted!' : createMutation.isPending ? 'Posting...' : 'Post Tee Time'}
          </Button>
        </form>

        {createMutation.isError && (() => {
          const error = createMutation.error;
          if (error instanceof ApiError && error.status === 409) {
            const duplicateError = error.data as DuplicateOpeningError;
            return (
              <p className="text-sm text-amber-600 mt-3">
                {duplicateError.error}
              </p>
            );
          }
          return (
            <p className="text-sm text-destructive mt-3">
              Couldn&apos;t post opening. Try again.
            </p>
          );
        })()}
      </CardContent>
    </Card>
  );
}
