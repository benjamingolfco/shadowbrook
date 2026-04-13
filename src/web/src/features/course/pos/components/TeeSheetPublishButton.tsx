import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { usePublishTeeSheet, useUnpublishTeeSheet } from '../hooks/useTeeSheetActions';
import { api } from '@/lib/api-client';
import { UnpublishTeeSheetDialog } from './UnpublishTeeSheetDialog';

interface TeeSheetPublishButtonProps {
  courseId: string;
  date: string;
  status: 'Draft' | 'Published' | null;
}

export function TeeSheetPublishButton({ courseId, date, status }: TeeSheetPublishButtonProps) {
  const [showDialog, setShowDialog] = useState(false);
  const [bookingCount, setBookingCount] = useState(0);
  const [checking, setChecking] = useState(false);
  const publishMutation = usePublishTeeSheet();
  const unpublishMutation = useUnpublishTeeSheet();

  if (status === null) {
    return null;
  }

  function handlePublish() {
    publishMutation.mutate({ courseId, date });
  }

  async function handleUnpublishClick() {
    setChecking(true);
    try {
      const result = await api.get<{ count: number }>(
        `/courses/${courseId}/tee-sheets/${date}/booking-count`,
      );
      if (result.count === 0) {
        unpublishMutation.mutate({ courseId, date, reason: null });
      } else {
        setBookingCount(result.count);
        setShowDialog(true);
      }
    } finally {
      setChecking(false);
    }
  }

  function handleUnpublishConfirm(reason: string | null) {
    unpublishMutation.mutate(
      { courseId, date, reason },
      { onSuccess: () => setShowDialog(false) },
    );
  }

  if (status === 'Draft') {
    return (
      <Button
        size="sm"
        onClick={handlePublish}
        disabled={publishMutation.isPending}
      >
        Publish
      </Button>
    );
  }

  return (
    <>
      <Button
        size="sm"
        variant="outline"
        onClick={handleUnpublishClick}
        disabled={unpublishMutation.isPending || checking}
      >
        Unpublish
      </Button>
      <UnpublishTeeSheetDialog
        open={showDialog}
        onOpenChange={setShowDialog}
        onConfirm={handleUnpublishConfirm}
        isPending={unpublishMutation.isPending}
        bookingCount={bookingCount}
      />
    </>
  );
}
