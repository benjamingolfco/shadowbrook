import { formatWallClockDate, formatWallClockTime } from '@/lib/course-time';
import type { WaitlistOfferAcceptResponse, WaitlistOfferResponse } from '@/types/waitlist';

interface AcceptConfirmationProps {
  response?: WaitlistOfferAcceptResponse;
  offer: WaitlistOfferResponse;
}

export default function AcceptConfirmation({ response, offer }: AcceptConfirmationProps) {
  const dateFormatted = formatWallClockDate(offer.teeTime);
  const timeFormatted = formatWallClockTime(offer.teeTime);

  return (
    <div className="space-y-6 text-center">
      <div
        className="mx-auto w-16 h-16 rounded-full bg-green-100 flex items-center justify-center"
        aria-hidden="true"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          className="w-8 h-8 text-green-600"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth={2.5}
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M20 6 9 17l-5-5" />
        </svg>
      </div>

      <div className="space-y-2">
        <h2 className="text-2xl font-bold">Tee Time Claimed</h2>
        {response?.message && <p className="text-muted-foreground">{response.message}</p>}
      </div>

      <div className="space-y-1">
        <p className="text-lg font-semibold">{offer.courseName}</p>
        <p className="text-muted-foreground">{dateFormatted} at {timeFormatted}</p>
      </div>
    </div>
  );
}
