import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { SmsMessage } from './useDevSms';

export function useDevGolferSms(golferId: string) {
  return useQuery({
    queryKey: queryKeys.devSms.byGolfer(golferId),
    queryFn: () => api.get<SmsMessage[]>(`/dev/sms/golfers/${golferId}`),
    refetchInterval: 5000,
    enabled: !!golferId,
  });
}
