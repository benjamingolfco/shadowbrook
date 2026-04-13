import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export function usePublishTeeSheet() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, date }: { courseId: string; date: string }) =>
      api.post<{ teeSheetId: string; status: string }>(
        `/courses/${courseId}/tee-sheets/${date}/publish`,
        {},
      ),
    onSuccess: (_, { courseId, date }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.teeSheets.byDate(courseId, date) });
    },
  });
}

export function useUnpublishTeeSheet() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      courseId,
      date,
      reason,
    }: {
      courseId: string;
      date: string;
      reason: string | null;
    }) =>
      api.post<{ teeSheetId: string; status: string }>(
        `/courses/${courseId}/tee-sheets/${date}/unpublish`,
        { reason },
      ),
    onSuccess: (_, { courseId, date }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.teeSheets.byDate(courseId, date) });
    },
  });
}

export function useBookingCount(courseId: string, date: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.teeSheets.bookingCount(courseId, date),
    queryFn: () =>
      api.get<{ count: number }>(`/courses/${courseId}/tee-sheets/${date}/booking-count`),
    enabled,
  });
}
