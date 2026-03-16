import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import type { WaitlistOfferResponse } from '@/types/waitlist';

interface OfferCardProps {
  offer: WaitlistOfferResponse;
  onAccept: () => void;
  isAccepting: boolean;
  acceptError: string | null;
}

export default function OfferCard({ offer, onAccept, isAccepting, acceptError }: OfferCardProps) {
  const firstName = offer.golferName.split(' ')[0];

  const dateParts = offer.date.split('-').map(Number);
  const date = new Date(dateParts[0] ?? 0, (dateParts[1] ?? 1) - 1, dateParts[2] ?? 1);
  const dateFormatted = date.toLocaleDateString('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  });

  const timeParts = offer.teeTime.split(':').map(Number);
  const timeFormatted = new Date(2000, 0, 1, timeParts[0] ?? 0, timeParts[1] ?? 0).toLocaleTimeString('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  });

  return (
    <Card>
      <CardContent className="pt-6 space-y-6">
        <div className="text-center space-y-2">
          <h2 className="text-2xl font-bold">{offer.courseName}</h2>
          <p className="text-lg text-muted-foreground">{dateFormatted}</p>
          <p className="text-3xl font-bold">{timeFormatted}</p>
          <p className="text-sm text-muted-foreground">
            {offer.golfersNeeded} {offer.golfersNeeded === 1 ? 'spot' : 'spots'} available
          </p>
        </div>

        <Separator />

        <div className="space-y-4">
          <p className="text-center text-lg">Hi, {firstName}!</p>

          <Button
            size="lg"
            className="w-full"
            onClick={onAccept}
            disabled={isAccepting}
          >
            {isAccepting ? 'Claiming...' : 'Claim This Tee Time'}
          </Button>

          {acceptError && (
            <p className="text-sm text-destructive text-center" role="alert">
              {acceptError}
            </p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
