import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type {
  WaitlistSettings,
  WaitlistResponse,
  CreateWaitlistRequest,
  WaitlistRequestEntry,
} from '@/types/waitlist';

export function useWaitlistSettings(courseId: string | undefined) {
  return useQuery({
    queryKey: courseId ? queryKeys.waitlist.settings(courseId) : ['disabled'],
    queryFn: () => api.get<WaitlistSettings>(`/courses/${courseId}/waitlist-settings`),
    enabled: !!courseId,
  });
}

export function useWaitlist(
  courseId: string | undefined,
  date: string,
  enabled: boolean = true,
) {
  return useQuery({
    queryKey: courseId ? queryKeys.waitlist.byDate(courseId, date) : ['disabled'],
    queryFn: () => api.get<WaitlistResponse>(`/courses/${courseId}/waitlist?date=${date}`),
    enabled: !!courseId && enabled,
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
    }) => api.post<WaitlistRequestEntry>(`/courses/${courseId}/waitlist/requests`, data),
    onSuccess: (_, { courseId, data }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.waitlist.byDate(courseId, data.date),
      });
    },
  });
}
