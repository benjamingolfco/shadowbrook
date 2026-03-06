import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type {
  CreateWaitlistRequest,
  WaitlistRequestEntry,
} from '@/types/waitlist';

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
