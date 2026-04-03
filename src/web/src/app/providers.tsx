import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { TooltipProvider } from '@/components/ui/tooltip';
import { ApiError } from '@/lib/api-client';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: (failureCount, error) => {
        if (error instanceof ApiError) {
          if (error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429) {
            return false;
          }
        }
        return failureCount < 5;
      },
      retryDelay: (attemptIndex) => Math.min(2000 * 2 ** attemptIndex, 30_000),
    },
    mutations: {
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status === 503) {
          return failureCount < 5;
        }
        return false;
      },
      retryDelay: (attemptIndex) => Math.min(2000 * 2 ** attemptIndex, 30_000),
    },
  },
});

export function Providers({ children }: { children: React.ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <TooltipProvider>
        {children}
      </TooltipProvider>
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
