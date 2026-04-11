import { useState } from 'react';
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
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';

interface UnpublishTeeSheetDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (reason: string | null) => void;
  isPending: boolean;
  bookingCount: number;
}

export function UnpublishTeeSheetDialog({
  open,
  onOpenChange,
  onConfirm,
  isPending,
  bookingCount,
}: UnpublishTeeSheetDialogProps) {
  const [reason, setReason] = useState('');

  function handleConfirm() {
    onConfirm(reason.trim() || null);
    setReason('');
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      setReason('');
    }
    onOpenChange(nextOpen);
  }

  return (
    <AlertDialog open={open} onOpenChange={handleOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Unpublish Tee Sheet?</AlertDialogTitle>
          <AlertDialogDescription>
            {bookingCount} booking(s) will be cancelled and golfers will be notified.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-2 py-2">
          <Label htmlFor="unpublish-reason">Reason (optional)</Label>
          <Textarea
            id="unpublish-reason"
            placeholder="e.g. Course maintenance"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            rows={2}
          />
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel autoFocus disabled={isPending}>
            Keep Published
          </AlertDialogCancel>
          <AlertDialogAction
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            onClick={handleConfirm}
            disabled={isPending}
          >
            Unpublish
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
