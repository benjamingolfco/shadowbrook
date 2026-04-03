import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export interface DeadLetterEnvelope {
  id: string;
  messageType: string;
  exceptionType: string;
  exceptionMessage: string;
  sentAt: string;
  replayable: boolean;
  source: string;
  receivedAt: string;
  message: unknown;
}

export interface DeadLettersPage {
  totalCount: number;
  envelopes: DeadLetterEnvelope[];
  pageNumber: number;
}

export function useDeadLetters(pageNumber = 1) {
  return useQuery({
    queryKey: [...queryKeys.deadLetters.all, pageNumber],
    queryFn: () =>
      api.post<DeadLettersPage[]>('/dead-letters/', {
        Limit: 50,
        PageNumber: pageNumber,
      }),
  });
}

export function useReplayDeadLetters() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) =>
      api.post<void>('/dead-letters/replay', { Ids: ids }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.deadLetters.all });
    },
  });
}

export function useDeleteDeadLetters() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) =>
      api.deleteWithBody('/dead-letters/', { Ids: ids }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.deadLetters.all });
    },
  });
}
