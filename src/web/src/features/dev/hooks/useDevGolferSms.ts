import { useQuery } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-keys';
import type { SmsMessage } from './useDevSms';

const BASE_URL = import.meta.env.VITE_API_URL ?? '';

// This hook is used from a public route (no AuthProvider). We deliberately
// do not use the shared api-client here, because it may attempt to acquire
// an MSAL token and, on stale caches, force a login redirect — which would
// bounce anonymous golfers to the sign-in page.
async function fetchGolferSms(golferId: string): Promise<SmsMessage[]> {
  const response = await fetch(`${BASE_URL}/dev/sms/golfers/${golferId}`);
  if (!response.ok) {
    throw new Error(`Failed to load SMS messages (${response.status})`);
  }
  return response.json() as Promise<SmsMessage[]>;
}

export function useDevGolferSms(golferId: string) {
  return useQuery({
    queryKey: queryKeys.devSms.byGolfer(golferId),
    queryFn: () => fetchGolferSms(golferId),
    refetchInterval: 5000,
    enabled: !!golferId,
  });
}
