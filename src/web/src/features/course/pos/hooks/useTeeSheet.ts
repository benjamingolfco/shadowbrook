import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { TeeSheetResponse } from '@/types/tee-time';

export function useTeeSheet(courseId: string, date: string) {
  return useQuery({
    queryKey: queryKeys.teeSheets.byDate(courseId, date),
    queryFn: () => api.get<TeeSheetResponse>(`/tee-sheets?courseId=${courseId}&date=${date}`),
  });
}
