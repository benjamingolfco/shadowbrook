import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type {
  AddGolferToWaitlistRequest,
  AddGolferToWaitlistResponse,
  CreateWaitlistRequest,
  WaitlistRequestEntry,
} from '@/types/waitlist';

export function useAddGolferToWaitlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      courseId,
      data,
    }: {
      courseId: string;
      data: AddGolferToWaitlistRequest;
    }) => api.post<AddGolferToWaitlistResponse>(`/courses/${courseId}/walkup-waitlist/entries`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.walkUpWaitlist.today(courseId),
      });
    },
  });
}

export function useCreateWaitlistRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      courseId,
      data,
    }: {
      courseId: string;
      data: CreateWaitlistRequest;
    }) => api.post<WaitlistRequestEntry>(`/courses/${courseId}/walkup-waitlist/requests`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.walkUpWaitlist.today(courseId),
      });
    },
  });
}

export function useRemoveGolferFromWaitlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, entryId }: { courseId: string; entryId: string }) =>
      api.delete(`/courses/${courseId}/walkup-waitlist/entries/${entryId}`),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.walkUpWaitlist.today(courseId),
      });
    },
  });
}
