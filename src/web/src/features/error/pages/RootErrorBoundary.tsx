import { isRouteErrorResponse, Link, useRouteError } from 'react-router';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/api-client';

export default function RootErrorBoundary() {
  const error = useRouteError();

  let heading = 'Something went wrong';
  let description = 'An unexpected error occurred. Please try again.';
  let detail: string | null = null;

  if (isRouteErrorResponse(error)) {
    if (error.status === 404) {
      heading = 'Page not found';
      description = "The page you're looking for doesn't exist or has been moved.";
    } else if (error.status === 401 || error.status === 403) {
      heading = 'Access Denied';
      description = "You don't have permission to view this page. If you believe this is a mistake, contact your administrator.";
    } else {
      description = `An error occurred (${error.status}). Please try again.`;
    }
  } else if (error instanceof ApiError) {
    if (error.status === 401 || error.status === 403) {
      heading = 'Access Denied';
      description = "You don't have permission to view this page. If you believe this is a mistake, contact your administrator.";
    } else if (error.status === 404) {
      heading = 'Page not found';
      description = "The page you're looking for doesn't exist or has been moved.";
    } else {
      description = `An error occurred (${error.status}). Please try again.`;
    }
    if (import.meta.env.DEV) {
      detail = error.message;
    }
  } else if (error instanceof Error) {
    if (import.meta.env.DEV) {
      detail = error.message;
    }
  }

  return (
    <div className="flex h-screen items-center justify-center">
      <div className="max-w-md space-y-4 p-8 text-center">
        <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
          {heading}
        </h1>
        <p className="text-muted-foreground">{description}</p>
        {detail && (
          <p className="text-sm text-muted-foreground font-mono bg-muted rounded px-3 py-2 text-left break-words">
            {detail}
          </p>
        )}
        <Button asChild variant="outline">
          <Link to="/">Go to Home</Link>
        </Button>
      </div>
    </div>
  );
}
