import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { PublishResponse } from '@/types/tee-time';

export function useUnpublishTeeSheet() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, date, reason }: { courseId: string; date: string; reason?: string }) =>
      api.post<PublishResponse>(`/courses/${courseId}/tee-sheets/${date}/unpublish`, { reason }),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.teeSheets.weeklyStatus(courseId, ''),
      });
    },
  });
}
