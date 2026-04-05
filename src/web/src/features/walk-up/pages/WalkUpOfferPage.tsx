import { useState } from 'react';
import { useParams } from 'react-router';
import { Skeleton } from '@/components/ui/skeleton';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
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
import { formatWallClockDate, formatWallClockTime } from '@/lib/course-time';
import { useWalkUpOffer, useAcceptOffer } from '../hooks/useWalkUpOffer';
import OfferCard from '../components/OfferCard';
import AcceptConfirmation from '../components/AcceptConfirmation';
import type { WaitlistOfferAcceptResponse, WaitlistOfferResponse } from '@/types/waitlist';

export default function WalkUpOfferPage() {
  const { token } = useParams<{ token: string }>();
  const [showConfirmDialog, setShowConfirmDialog] = useState(false);
  const [acceptResult, setAcceptResult] = useState<{ response: WaitlistOfferAcceptResponse; offer: WaitlistOfferResponse } | null>(null);
  const [acceptError, setAcceptError] = useState<string | null>(null);

  const { data: offer, isLoading, error, refetch } = useWalkUpOffer(token ?? '');
  const acceptOffer = useAcceptOffer(token ?? '');

  function handleAcceptClick() {
    setShowConfirmDialog(true);
  }

  function handleConfirmAccept() {
    setShowConfirmDialog(false);

    acceptOffer.mutate(undefined, {
      onSuccess: (data) => {
        setAcceptResult({ response: data, offer: offer! });
        setAcceptError(null);
      },
      onError: (err) => {
        const errorMessage = err instanceof Error ? err.message : 'Failed to claim tee time';
        setAcceptError(errorMessage);
      },
    });
  }

  // Success state
  if (acceptResult) {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-sm">
          <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>
          <AcceptConfirmation response={acceptResult.response} offer={acceptResult.offer} />
        </div>
      </div>
    );
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-sm">
          <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>
          <Card>
            <CardContent className="pt-6 space-y-6">
              <div className="space-y-2">
                <Skeleton className="h-8 w-3/4 mx-auto" />
                <Skeleton className="h-6 w-1/2 mx-auto" />
                <Skeleton className="h-10 w-2/3 mx-auto" />
                <Skeleton className="h-4 w-1/3 mx-auto" />
              </div>
              <Skeleton className="h-4 w-1/2 mx-auto" />
              <Skeleton className="h-px w-full" />
              <div className="space-y-4">
                <Skeleton className="h-6 w-1/4 mx-auto" />
                <Skeleton className="h-11 w-full" />
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Not found state
  if (error && (error as Error & { status?: number }).status === 404) {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-sm">
          <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>
          <Card>
            <CardContent className="pt-6 text-center space-y-4">
              <h2 className="text-xl font-semibold">Offer Not Found</h2>
              <p className="text-muted-foreground">
                This tee time offer could not be found. It may have already been claimed or expired.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Generic error state
  if (error || !offer) {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-sm">
          <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>
          <Card>
            <CardContent className="pt-6 text-center space-y-4">
              <h2 className="text-xl font-semibold">Something Went Wrong</h2>
              <p className="text-muted-foreground">
                {error instanceof Error ? error.message : 'Unable to load tee time offer'}
              </p>
              <Button onClick={() => refetch()}>Try Again</Button>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Rejected offer
  if (offer.status === 'Rejected') {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-sm">
          <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>
          <Card>
            <CardContent className="pt-6 text-center space-y-4">
              <h2 className="text-xl font-semibold">Offer No Longer Available</h2>
              <p className="text-muted-foreground">
                This tee time offer is no longer available. It may have expired or been filled.
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    );
  }

  // Already accepted (e.g. page reload after claiming)
  if (offer.status === 'Accepted') {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
        <div className="w-full max-w-sm">
          <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>
          <AcceptConfirmation offer={offer} />
        </div>
      </div>
    );
  }

  // Format for confirmation dialog
  const dateFormatted = formatWallClockDate(offer.teeTime);
  const timeFormatted = formatWallClockTime(offer.teeTime);

  return (
    <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm">
        <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>

        <OfferCard
          offer={offer}
          onAccept={handleAcceptClick}
          isAccepting={acceptOffer.isPending}
          acceptError={acceptError}
        />

        <AlertDialog open={showConfirmDialog} onOpenChange={setShowConfirmDialog}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Claim this tee time?</AlertDialogTitle>
              <AlertDialogDescription>
                {offer.courseName} at {timeFormatted} on {dateFormatted}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={handleConfirmAccept}>
                Confirm
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </div>
  );
}
