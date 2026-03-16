import type { WaitlistOfferAcceptResponse } from '@/types/waitlist';

interface AcceptConfirmationProps {
  response: WaitlistOfferAcceptResponse;
}

export default function AcceptConfirmation({ response }: AcceptConfirmationProps) {
  return (
    <div className="space-y-6 text-center">
      <div
        className="mx-auto w-16 h-16 rounded-full bg-blue-100 flex items-center justify-center"
        aria-hidden="true"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          className="w-8 h-8 text-blue-600"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth={2.5}
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <path d="M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z" />
          <path d="M12 8v4" />
          <path d="M12 16h.01" />
        </svg>
      </div>

      <div className="space-y-2">
        <h2 className="text-2xl font-bold">Request Received</h2>
        <p className="text-muted-foreground">
          {response.message}
        </p>
      </div>

      <p className="text-sm text-muted-foreground">
        We're processing your request — you'll receive a confirmation text shortly.
      </p>
    </div>
  );
}
