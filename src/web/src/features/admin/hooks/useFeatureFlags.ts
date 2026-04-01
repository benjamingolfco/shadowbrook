import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export function useSetOrgFeatures() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ orgId, flags }: { orgId: string; flags: Record<string, boolean> }) =>
      api.put<Record<string, boolean>>(`/organizations/${orgId}/features`, { flags }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['features'] });
    },
  });
}

export function useSetCourseFeatures() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, flags }: { courseId: string; flags: Record<string, boolean> }) =>
      api.put<Record<string, boolean>>(`/courses/${courseId}/features`, { flags }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['features'] });
      void queryClient.invalidateQueries({ queryKey: queryKeys.features.all });
    },
  });
}
