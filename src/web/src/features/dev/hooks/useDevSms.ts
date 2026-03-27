import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
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

export function useDeleteConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (phoneNumber: string) =>
      api.delete(`/dev/sms/conversations/${encodeURIComponent(phoneNumber)}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.devSms.all,
      });
    },
  });
}
