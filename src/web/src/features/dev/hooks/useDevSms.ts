import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export type SmsDirection = 0 | 1;

export interface SmsMessage {
  from: string;
  to: string;
  body: string;
  timestamp: string;
  direction: SmsDirection;
}

export function useDevSms() {
  return useQuery({
    queryKey: queryKeys.devSms.all,
    queryFn: () => api.get<SmsMessage[]>('/dev/sms'),
    refetchInterval: 5000,
  });
}
