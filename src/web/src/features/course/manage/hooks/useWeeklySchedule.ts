import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { WeeklyStatusResponse } from '@/types/tee-time';

export function useWeeklySchedule(courseId: string, startDate: string) {
  return useQuery({
    queryKey: queryKeys.teeSheets.weeklyStatus(courseId, startDate),
    queryFn: () => api.get<WeeklyStatusResponse>(
      `/courses/${courseId}/tee-sheets/week?startDate=${startDate}`
    ),
  });
}
