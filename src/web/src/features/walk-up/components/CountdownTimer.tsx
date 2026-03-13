import { useState, useEffect } from 'react';

interface CountdownTimerProps {
  expiresAt: string;
  onExpired: () => void;
}

export default function CountdownTimer({ expiresAt, onExpired }: CountdownTimerProps) {
  const [timeLeft, setTimeLeft] = useState<number>(() => {
    const expiry = new Date(expiresAt).getTime();
    const now = Date.now();
    return Math.max(0, Math.floor((expiry - now) / 1000));
  });

  useEffect(() => {
    const expiry = new Date(expiresAt).getTime();

    const interval = setInterval(() => {
      const now = Date.now();
      const remaining = Math.max(0, Math.floor((expiry - now) / 1000));
      setTimeLeft(remaining);

      if (remaining === 0) {
        clearInterval(interval);
        onExpired();
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [expiresAt, onExpired]);

  if (timeLeft === 0) {
    return null;
  }

  const minutes = Math.floor(timeLeft / 60);
  const seconds = timeLeft % 60;
  const displayText = minutes >= 5
    ? `${minutes} ${minutes === 1 ? 'minute' : 'minutes'} remaining`
    : `${minutes}:${seconds.toString().padStart(2, '0')} remaining`;

  // For screen readers, only announce changes every 30 seconds to avoid fatigue
  const ariaText = Math.floor(timeLeft / 30) % 2 === 0
    ? displayText
    : undefined;

  return (
    <div className="text-center">
      <p
        className="text-sm font-medium text-orange-600"
        role="timer"
        aria-live="polite"
        aria-atomic="true"
      >
        {displayText}
        {ariaText && <span className="sr-only">{ariaText}</span>}
      </p>
    </div>
  );
}
