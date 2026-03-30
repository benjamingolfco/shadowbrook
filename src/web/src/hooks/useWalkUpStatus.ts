import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export interface WalkUpQrStatus {
  status: 'open' | 'closed' | 'expired';
  courseName: string;
  date: string;
}

export function useWalkUpStatus(shortCode: string | undefined) {
  return useQuery({
    queryKey: queryKeys.walkUpQr.status(shortCode ?? ''),
    queryFn: () => api.get<WalkUpQrStatus>(`/walkup/status/${shortCode}`),
    enabled: !!shortCode,
  });
}
