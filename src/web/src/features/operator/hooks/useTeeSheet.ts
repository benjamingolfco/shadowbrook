import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { TeeSheetResponse } from '@/types/tee-time';

export function useTeeSheet(courseId: string | undefined, date: string) {
  return useQuery({
    queryKey: courseId ? queryKeys.teeSheets.byDate(courseId, date) : ['disabled'],
    queryFn: () => api.get<TeeSheetResponse>(`/tee-sheets?courseId=${courseId}&date=${date}`),
    enabled: !!courseId,
  });
}
