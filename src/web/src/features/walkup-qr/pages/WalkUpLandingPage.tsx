import { useEffect } from 'react';
import { useParams, Link } from 'react-router';
import { Clock, CalendarX2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useWalkUpStatus } from '../hooks/useWalkUpStatus';

export default function WalkUpLandingPage() {
  const { shortCode } = useParams<{ shortCode: string }>();
  const { data, isLoading, isError, error, refetch } = useWalkUpStatus(shortCode);

  useEffect(() => {
    if (data) {
      document.title = `Walk-Up Waitlist - ${data.courseName}`;
    } else {
      document.title = 'Walk-Up Waitlist';
    }
  }, [data]);

  // Loading state
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

  // Error state - network error
  if (isError) {
    const errorWithStatus = error as Error & { status?: number };

    // 404 means invalid/expired code - show as expired state below
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

    // Network or other error
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

  if (!data) {
    return null;
  }

  // Open state - redirect to join flow
  if (data.status === 'open') {
    return (
      <div className="min-h-dvh flex items-center justify-center px-4 py-8">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle>{data.courseName}</CardTitle>
            <CardDescription>Walk-Up Waitlist</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm">
              The walk-up waitlist is open for {(() => {
                const [year, month, day] = data.date.split('-');
                return `${parseInt(month ?? '')}/${parseInt(day ?? '')}/${year}`;
              })()}.
            </p>
            <Button asChild className="w-full">
              <Link to={`/join/${shortCode}`}>Join Waitlist</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Closed state
  if (data.status === 'closed') {
    return (
      <div className="min-h-dvh flex items-center justify-center px-4 py-8">
        <Card className="w-full max-w-md">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5" aria-hidden="true" />
              Waitlist Closed
            </CardTitle>
            <CardDescription>{data.courseName}</CardDescription>
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

  // Expired state
  return (
    <div className="min-h-dvh flex items-center justify-center px-4 py-8">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <CalendarX2 className="h-5 w-5" aria-hidden="true" />
            Code Expired
          </CardTitle>
          <CardDescription>{data.courseName}</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            This QR code is no longer valid. It was valid on {(() => {
              const [year, month, day] = data.date.split('-');
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
