import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import type { MeResponse } from '../types';

export function useMe(enabled: boolean) {
  return useQuery({
    queryKey: ['auth', 'me'],
    queryFn: () => api.get<MeResponse>('/auth/me'),
    enabled,
    refetchOnWindowFocus: true,
    staleTime: 5 * 60 * 1000,
  });
}
