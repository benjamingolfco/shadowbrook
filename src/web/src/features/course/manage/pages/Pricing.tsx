import { useState } from 'react';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { useCourseId } from '../../hooks/useCourseId';
import {
  usePricing,
  useUpdateDefaultPrice,
  useUpdateBounds,
  useCreateSchedule,
  useUpdateSchedule,
  useDeleteSchedule,
} from '../hooks/usePricing';
import { DefaultPriceCard } from '../components/DefaultPriceCard';
import { PriceBoundsCard } from '../components/PriceBoundsCard';
import { RateScheduleList } from '../components/RateScheduleList';
import { RateScheduleDialog, type RateScheduleFormData } from '../components/RateScheduleDialog';
import type { ApiError } from '@/lib/api-client';
import type { RateSchedule } from '@/types/course';

export default function Pricing() {
  const courseId = useCourseId();
  const pricingQuery = usePricing(courseId);
  const updateDefaultPrice = useUpdateDefaultPrice();
  const updateBounds = useUpdateBounds();
  const createSchedule = useCreateSchedule();
  const updateScheduleMutation = useUpdateSchedule();
  const deleteSchedule = useDeleteSchedule();

  const [showBoundsSetup, setShowBoundsSetup] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingSchedule, setEditingSchedule] = useState<RateSchedule | null>(null);
  const [scheduleError, setScheduleError] = useState<string | null>(null);

  const pricing = pricingQuery.data;
  const hasBounds = pricing?.minPrice !== null && pricing?.maxPrice !== null;

  function handleSaveDefaultPrice(price: number) {
    updateDefaultPrice.mutate({ courseId, defaultPrice: price });
  }

  function handleSaveBounds(min: number, max: number) {
    updateBounds.mutate({ courseId, minPrice: min, maxPrice: max });
    setShowBoundsSetup(false);
  }

  function handleOpenCreate() {
    setEditingSchedule(null);
    setScheduleError(null);
    setDialogOpen(true);
  }

  function handleOpenEdit(schedule: RateSchedule) {
    setEditingSchedule(schedule);
    setScheduleError(null);
    setDialogOpen(true);
  }

  function handleSaveSchedule(data: RateScheduleFormData) {
    setScheduleError(null);
    const scheduleData = {
      name: data.name,
      daysOfWeek: data.daysOfWeek,
      startTime: data.startTime,
      endTime: data.endTime,
      price: data.price,
    };

    const onError = (error: Error) => {
      const apiError = error as ApiError;
      setScheduleError(apiError.message);
    };

    const onSuccess = () => {
      setDialogOpen(false);
      setEditingSchedule(null);
    };

    if (editingSchedule) {
      updateScheduleMutation.mutate(
        { courseId, scheduleId: editingSchedule.id, data: scheduleData },
        { onSuccess, onError },
      );
    } else {
      createSchedule.mutate(
        { courseId, data: scheduleData },
        { onSuccess, onError },
      );
    }
  }

  function handleDeleteSchedule(scheduleId: string) {
    deleteSchedule.mutate({ courseId, scheduleId });
  }

  if (pricingQuery.isLoading) {
    return (
      <>
        <PageTopbar
          middle={<h1 className="font-display text-[18px] text-ink">Pricing</h1>}
        />
        <div className="max-w-2xl">
          <p className="text-ink-muted text-sm">Loading pricing...</p>
        </div>
      </>
    );
  }

  if (pricingQuery.isError) {
    return (
      <>
        <PageTopbar
          middle={<h1 className="font-display text-[18px] text-ink">Pricing</h1>}
        />
        <div className="max-w-2xl">
          <p className="text-destructive text-sm">Error loading pricing: {pricingQuery.error.message}</p>
        </div>
      </>
    );
  }

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Pricing</h1>}
      />

      <div className="max-w-2xl space-y-4">
        {/* Default Price — always visible */}
        <DefaultPriceCard
          defaultPrice={pricing?.defaultPrice ?? null}
          minPrice={pricing?.minPrice ?? null}
          maxPrice={pricing?.maxPrice ?? null}
          onSave={handleSaveDefaultPrice}
          isPending={updateDefaultPrice.isPending}
        />

        {/* Price Bounds — shown when configured, or during setup */}
        {hasBounds || showBoundsSetup ? (
          <PriceBoundsCard
            minPrice={pricing?.minPrice ?? null}
            maxPrice={pricing?.maxPrice ?? null}
            onSave={handleSaveBounds}
            isPending={updateBounds.isPending}
            initialEdit={showBoundsSetup && !hasBounds}
          />
        ) : (
          <Card className="border-dashed">
            <CardContent className="text-center py-6">
              <p className="text-sm text-muted-foreground mb-3">
                Want different prices for different times?
              </p>
              <Button variant="outline" onClick={() => setShowBoundsSetup(true)}>
                Set Up Price Bounds
              </Button>
            </CardContent>
          </Card>
        )}

        {/* Rate Schedules — only shown after bounds configured */}
        {hasBounds && (
          <RateScheduleList
            schedules={pricing?.schedules ?? []}
            onAdd={handleOpenCreate}
            onEdit={handleOpenEdit}
            onDelete={handleDeleteSchedule}
            isDeleting={deleteSchedule.isPending}
          />
        )}
      </div>

      {/* Schedule dialog */}
      {hasBounds && (
        <RateScheduleDialog
          open={dialogOpen}
          onOpenChange={setDialogOpen}
          onSave={handleSaveSchedule}
          isPending={createSchedule.isPending || updateScheduleMutation.isPending}
          schedule={editingSchedule}
          minPrice={pricing!.minPrice!}
          maxPrice={pricing!.maxPrice!}
          serverError={scheduleError}
        />
      )}
    </>
  );
}
