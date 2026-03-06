import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { WalkUpWaitlist, WalkUpWaitlistTodayResponse } from '@/types/waitlist';

export function useWalkUpWaitlistToday(courseId: string | undefined) {
  return useQuery({
    queryKey: courseId ? queryKeys.walkUpWaitlist.today(courseId) : ['disabled'],
    queryFn: () => api.get<WalkUpWaitlistTodayResponse>(`/courses/${courseId}/walkup-waitlist/today`),
    enabled: !!courseId,
    refetchInterval: 30000,
  });
}

export function useOpenWalkUpWaitlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId }: { courseId: string }) =>
      api.post<WalkUpWaitlist>(`/courses/${courseId}/walkup-waitlist/open`, {}),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.walkUpWaitlist.today(courseId) });
    },
  });
}

export function useCloseWalkUpWaitlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId }: { courseId: string }) =>
      api.post<WalkUpWaitlist>(`/courses/${courseId}/walkup-waitlist/close`, {}),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.walkUpWaitlist.today(courseId) });
    },
  });
}
