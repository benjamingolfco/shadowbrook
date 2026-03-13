import type { WaitlistOfferAcceptResponse } from '@/types/waitlist';

interface AcceptConfirmationProps {
  response: WaitlistOfferAcceptResponse;
}

export default function AcceptConfirmation({ response }: AcceptConfirmationProps) {
  const firstName = response.golferName.split(' ')[0];

  // Format date
  const date = new Date(response.date);
  const dateFormatted = date.toLocaleDateString('en-US', {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
  });

  // Format time
  const [hours, minutes] = response.teeTime.split(':').map(Number);
  const timeFormatted = new Date(2000, 0, 1, hours, minutes).toLocaleTimeString('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  });

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
        <h2 className="text-2xl font-bold">You're booked, {firstName}!</h2>
        <p className="text-lg font-medium">{response.courseName}</p>
        <p className="text-lg text-muted-foreground">{dateFormatted}</p>
        <p className="text-2xl font-bold">{timeFormatted}</p>
      </div>

      <p className="text-sm text-muted-foreground">
        See you on the course!
      </p>
    </div>
  );
}
