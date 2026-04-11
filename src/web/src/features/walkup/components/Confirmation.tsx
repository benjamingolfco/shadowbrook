import { Link } from 'react-router';
import { MessageSquareText } from 'lucide-react';
import type { JoinWaitlistResponse } from '@/types/waitlist';

interface ConfirmationProps {
  result: JoinWaitlistResponse;
  phone?: string;
}

export default function Confirmation({ result, phone }: ConfirmationProps) {
  const firstName = result.golferName.split(' ')[0];
  const smsLinkId = result.golferId || (phone ?? '');

  return (
    <div className="space-y-6 text-center">
      <div
        className="mx-auto w-16 h-16 rounded-full bg-green-light flex items-center justify-center"
        aria-hidden="true"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          className="w-8 h-8 text-green"
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

      {(import.meta.env.DEV || import.meta.env.VITE_SHOW_DEV_TOOLS === 'true') && (
        <div className="pt-2">
          <Link
            to={`/dev/sms/golfer/${smsLinkId}`}
            className="inline-flex items-center gap-2 rounded-md border-2 border-dashed border-orange bg-orange-faint px-4 py-2 text-sm font-semibold text-foreground hover:bg-orange-light transition-colors"
          >
            <MessageSquareText className="h-4 w-4" />
            View SMS messages
            <span className="text-[10px] font-mono uppercase tracking-wide bg-orange-light text-orange rounded px-1.5 py-0.5">
              Dev
            </span>
          </Link>
        </div>
      )}
    </div>
  );
}
