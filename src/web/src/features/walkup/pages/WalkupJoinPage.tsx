import { useState, useEffect } from 'react';
import { useParams } from 'react-router';
import { Clock, CalendarX2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useWalkUpStatus } from '@/hooks/useWalkUpStatus';
import CodeEntry from '../components/CodeEntry';
import JoinForm from '../components/JoinForm';
import Confirmation from '../components/Confirmation';
import type { VerifyCodeResponse, JoinWaitlistResponse } from '@/types/waitlist';

type Phase = 'code' | 'join' | 'confirmation';

export default function WalkupJoinPage() {
  const { shortCode } = useParams<{ shortCode: string }>();
  const [phase, setPhase] = useState<Phase>('code');
  const [verifyData, setVerifyData] = useState<VerifyCodeResponse | null>(null);
  const [joinResult, setJoinResult] = useState<JoinWaitlistResponse | null>(null);
  const [submittedPhone, setSubmittedPhone] = useState('');

  const { data: statusData, isLoading, isError, error, refetch } = useWalkUpStatus(shortCode);

  const courseName =
    joinResult?.courseName ?? verifyData?.courseName ?? statusData?.courseName;

  useEffect(() => {
    if (courseName) {
      document.title = `${courseName} – Join Waitlist`;
    } else {
      document.title = 'Join Waitlist';
    }
    return () => {
      document.title = 'Teeforce';
    };
  }, [courseName]);

  function handleVerified(data: VerifyCodeResponse) {
    setVerifyData(data);
    setPhase('join');
  }

  function handleJoined(result: JoinWaitlistResponse) {
    setJoinResult(result);
    setPhase('confirmation');
  }

  // Only run status checks when a shortCode is present in the URL
  if (shortCode) {
    if (isLoading) {
      return (
        <div className="min-h-dvh flex items-center justify-center px-4 py-8">
          <Card className="w-full max-w-md">
            <CardHeader>
              <Skeleton className="h-6 w-48 mb-2" />
              <Skeleton className="h-4 w-32" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-10 w-full" />
            </CardContent>
          </Card>
          <span className="sr-only">Loading waitlist status</span>
        </div>
      );
    }

    if (isError) {
      const errorWithStatus = error as Error & { status?: number };

      if (errorWithStatus.status === 404) {
        return (
          <div className="min-h-dvh flex items-center justify-center px-4 py-8">
            <Card className="w-full max-w-md">
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <CalendarX2 className="h-5 w-5" aria-hidden="true" />
                  Invalid Code
                </CardTitle>
                <CardDescription>This QR code is not valid.</CardDescription>
              </CardHeader>
              <CardContent>
                <p className="text-sm text-muted-foreground">
                  Please ask for the current walk-up waitlist code at the pro shop.
                </p>
              </CardContent>
            </Card>
          </div>
        );
      }

      return (
        <div className="min-h-dvh flex items-center justify-center px-4 py-8">
          <Card className="w-full max-w-md">
            <CardHeader>
              <CardTitle>Something went wrong</CardTitle>
              <CardDescription>Unable to load waitlist status</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-muted-foreground">
                {errorWithStatus.message || 'Please try again.'}
              </p>
              <Button onClick={() => void refetch()} className="w-full">
                Try Again
              </Button>
            </CardContent>
          </Card>
        </div>
      );
    }

    if (statusData?.status === 'closed') {
      return (
        <div className="min-h-dvh flex items-center justify-center px-4 py-8">
          <Card className="w-full max-w-md">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Clock className="h-5 w-5" aria-hidden="true" />
                Waitlist Closed
              </CardTitle>
              <CardDescription>{statusData.courseName}</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                The walk-up waitlist is closed for today. No new entries are being accepted.
              </p>
            </CardContent>
          </Card>
        </div>
      );
    }

    if (statusData?.status === 'expired') {
      return (
        <div className="min-h-dvh flex items-center justify-center px-4 py-8">
          <Card className="w-full max-w-md">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CalendarX2 className="h-5 w-5" aria-hidden="true" />
                Code Expired
              </CardTitle>
              <CardDescription>{statusData.courseName}</CardDescription>
            </CardHeader>
            <CardContent>
              <p className="text-sm text-muted-foreground">
                This QR code is no longer valid. It was valid on {(() => {
                  const [year, month, day] = statusData.date.split('-');
                  return `${parseInt(month ?? '')}/${parseInt(day ?? '')}/${year}`;
                })()}.
              </p>
              <p className="text-sm text-muted-foreground mt-2">
                Please ask for today's code at the pro shop.
              </p>
            </CardContent>
          </Card>
        </div>
      );
    }
  }

  return (
    <div className="min-h-dvh flex flex-col items-center justify-center px-4 py-8">
      <div className="w-full max-w-sm">
        <h1 className="text-xl font-bold text-center mb-8">Teeforce</h1>

        {phase === 'code' && <CodeEntry onVerified={handleVerified} initialCode={shortCode} />}
        {phase === 'join' && verifyData && (
          <JoinForm verifyData={verifyData} onJoined={handleJoined} onPhoneCapture={setSubmittedPhone} />
        )}
        {phase === 'confirmation' && joinResult && (
          <Confirmation result={joinResult} phone={submittedPhone} />
        )}
      </div>
    </div>
  );
}
