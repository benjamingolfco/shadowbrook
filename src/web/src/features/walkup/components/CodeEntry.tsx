import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { useVerifyCode } from '../hooks/useWalkupJoin';
import type { VerifyCodeResponse } from '@/types/waitlist';

interface CodeEntryProps {
  onVerified: (data: VerifyCodeResponse) => void;
}

export default function CodeEntry({ onVerified }: CodeEntryProps) {
  const [code, setCode] = useState('');
  const verifyMutation = useVerifyCode();

  function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
    const raw = e.target.value.replace(/\D/g, '').slice(0, 4);
    setCode(raw);

    if (raw.length === 4) {
      verifyMutation.mutate(raw, {
        onSuccess: (data) => {
          onVerified(data);
        },
      });
    }
  }

  function getErrorMessage() {
    if (!verifyMutation.isError) return null;
    const err = verifyMutation.error as Error & { status?: number };
    if (err.status === 404) {
      return 'Code not found. Check the code posted at the course and try again.';
    }
    return 'Something went wrong. Please try again.';
  }

  const errorMessage = getErrorMessage();

  return (
    <div className="space-y-4">
      <div className="text-center space-y-2">
        <h2 className="text-lg font-semibold">Enter your code</h2>
        <p className="text-sm text-muted-foreground">
          Enter the 4-digit code posted at the course
        </p>
      </div>

      <Input
        inputMode="numeric"
        pattern="[0-9]*"
        maxLength={4}
        autoFocus
        placeholder="0000"
        value={code}
        onChange={handleChange}
        disabled={verifyMutation.isPending}
        className="text-center text-3xl font-mono tracking-widest h-16"
        aria-label="4-digit course code"
        aria-describedby={errorMessage ? 'code-error' : undefined}
      />

      {verifyMutation.isPending && (
        <p className="text-center text-sm text-muted-foreground" aria-live="polite">
          Verifying...
        </p>
      )}

      {errorMessage && (
        <p id="code-error" className="text-sm text-destructive text-center" role="alert">
          {errorMessage}
        </p>
      )}
    </div>
  );
}
