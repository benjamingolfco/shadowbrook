import { useRef, useState } from 'react';
import { useVerifyCode } from '../hooks/useVerifyCode';

interface CodeEntryProps {
  onVerified: (data: { courseWaitlistId: string; courseName: string }) => void;
}

export default function CodeEntry({ onVerified }: CodeEntryProps) {
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const verifyMutation = useVerifyCode();

  function getErrorMessage(status?: number): string {
    if (status === 404) {
      return 'Code not found. Check the code posted at the course and try again.';
    }
    if (status === 410) {
      return 'This waitlist code has expired. Ask the pro shop for today\'s code.';
    }
    return 'Something went wrong. Please try again.';
  }

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const value = e.target.value.replace(/\D/g, '').slice(0, 4);
    setCode(value);
    setError(null);

    if (value.length === 4) {
      verifyMutation.mutate(value, {
        onSuccess: (data) => {
          onVerified({
            courseWaitlistId: data.courseWaitlistId,
            courseName: data.courseName,
          });
        },
        onError: (err) => {
          const errorWithStatus = err as Error & { status?: number };
          setError(getErrorMessage(errorWithStatus.status));
          setCode('');
          setTimeout(() => {
            inputRef.current?.focus();
          }, 0);
        },
      });
    }
  }

  return (
    <div className="w-full max-w-xs">
      <div className="mb-4 text-center">
        <label
          htmlFor="walkup-code"
          className="block text-base font-medium text-gray-900 mb-1"
        >
          Enter the 4-digit code
        </label>
        <p className="text-sm text-gray-500">
          Look for the code posted at the starter&apos;s booth or pro shop
        </p>
      </div>

      <input
        ref={inputRef}
        id="walkup-code"
        type="text"
        inputMode="numeric"
        pattern="[0-9]*"
        maxLength={4}
        value={code}
        onChange={handleChange}
        disabled={verifyMutation.isPending}
        autoComplete="off"
        autoFocus
        className="w-full h-14 text-center text-3xl tracking-[0.5em] font-mono border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent disabled:opacity-50"
        aria-describedby={error ? 'code-error' : undefined}
        aria-invalid={error ? true : undefined}
      />

      {verifyMutation.isPending && (
        <p className="mt-2 text-center text-sm text-gray-500" aria-live="polite">
          Verifying...
        </p>
      )}

      {error && (
        <p
          id="code-error"
          className="mt-2 text-center text-sm text-destructive"
          role="alert"
        >
          {error}
        </p>
      )}
    </div>
  );
}
