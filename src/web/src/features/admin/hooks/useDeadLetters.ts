import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export interface DeadLetterMessage {
  Id: string;
  MessageType: string;
  ExceptionType: string;
  ExceptionMessage: string;
  SentAt: string;
  Body: unknown;
}

export interface DeadLettersResponse {
  Messages: DeadLetterMessage[];
  NextId: string | null;
}

export function useDeadLetters(cursor?: string) {
  return useQuery({
    queryKey: [...queryKeys.deadLetters.all, cursor],
    queryFn: () =>
      api.post<DeadLettersResponse>('/dead-letters/', {
        Limit: 50,
        ...(cursor ? { NextId: cursor } : {}),
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
