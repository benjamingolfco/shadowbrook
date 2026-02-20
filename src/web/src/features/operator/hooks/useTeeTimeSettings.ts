import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Course, TeeTimeSettings } from '@/types/course';

export function useCourses(tenantId: string) {
  return useQuery({
    queryKey: queryKeys.courses.all(tenantId),
    queryFn: () => api.get<Course[]>('/courses'),
  });
}

export function useTeeTimeSettings(courseId: string | undefined) {
  return useQuery({
    queryKey: courseId ? queryKeys.courses.settings(courseId) : ['disabled'],
    queryFn: () => api.get<TeeTimeSettings>(`/courses/${courseId}/tee-time-settings`),
    enabled: !!courseId,
  });
}

export function useUpdateTeeTimeSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, data }: { courseId: string; data: Omit<TeeTimeSettings, 'courseId'> }) =>
      api.put<TeeTimeSettings>(`/courses/${courseId}/tee-time-settings`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.settings(courseId) });
    },
  });
}
