import type { JoinWaitlistResponse } from '@/types/waitlist';

interface ConfirmationProps {
  result: JoinWaitlistResponse;
}

export default function Confirmation({ result }: ConfirmationProps) {
  const firstName = result.golferName.split(' ')[0];

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
        <h2 className="text-2xl font-bold">You're on the list, {firstName}!</h2>
        <p className="text-lg text-muted-foreground">
          {result.position > 0
            ? `#${result.position} in line at ${result.courseName}`
            : result.courseName}
        </p>
      </div>

      <p className="text-sm text-muted-foreground">
        Keep your phone handy — we'll text you when a spot opens up.
      </p>
    </div>
  );
}
