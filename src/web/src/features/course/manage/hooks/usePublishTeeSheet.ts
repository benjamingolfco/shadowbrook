import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { PublishResponse } from '@/types/tee-time';

export function usePublishTeeSheet() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, date }: { courseId: string; date: string }) =>
      api.post<PublishResponse>(`/courses/${courseId}/tee-sheets/${date}/publish`, {}),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeys.teeSheets.weeklyStatus(courseId, ''),
      });
    },
  });
}
