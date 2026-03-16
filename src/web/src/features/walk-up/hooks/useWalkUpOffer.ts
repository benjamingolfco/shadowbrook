import { useQuery, useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { WaitlistOfferResponse, WaitlistOfferAcceptResponse } from '@/types/waitlist';

export function useWalkUpOffer(token: string) {
  return useQuery({
    queryKey: queryKeys.walkUpOffer.byToken(token),
    queryFn: () => api.get<WaitlistOfferResponse>(`/waitlist/offers/${token}`),
    enabled: !!token,
    refetchOnWindowFocus: false,
  });
}

export function useAcceptOffer(token: string) {
  return useMutation({
    mutationFn: () => api.post<WaitlistOfferAcceptResponse>(`/waitlist/offers/${token}/accept`, {}),
  });
}
