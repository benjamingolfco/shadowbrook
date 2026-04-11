import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import type { BulkDraftResponse } from '@/types/tee-time';

export function useBulkDraft() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, dates }: { courseId: string; dates: string[] }) =>
      api.post<BulkDraftResponse>(`/courses/${courseId}/tee-sheets/draft`, { dates }),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({
        queryKey: ['tee-sheets', courseId],
      });
    },
  });
}
