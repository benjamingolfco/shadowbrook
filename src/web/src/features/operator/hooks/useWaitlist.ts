import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type {
  AddGolferToWaitlistRequest,
  AddGolferToWaitlistResponse,
  CreateTeeTimeOpeningRequest,
  WaitlistOpeningEntry,
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

export function useCreateTeeTimeOpening() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      courseId,
      data,
    }: {
      courseId: string;
      data: CreateTeeTimeOpeningRequest;
    }) => api.post<WaitlistOpeningEntry>(`/courses/${courseId}/tee-time-openings`, data),
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

export function useCancelTeeTimeOpening() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, openingId }: { courseId: string; openingId: string }) =>
      api.post<WaitlistOpeningEntry>(`/courses/${courseId}/tee-time-openings/${openingId}/cancel`, {}),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.walkUpWaitlist.today(courseId),
      });
    },
  });
}
