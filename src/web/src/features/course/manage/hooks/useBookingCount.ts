import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { BookingCountResponse } from '@/types/tee-time';

export function useBookingCount(courseId: string, date: string, enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.teeSheets.bookingCount(courseId, date),
    queryFn: () => api.get<BookingCountResponse>(`/courses/${courseId}/tee-sheets/${date}/booking-count`),
    enabled,
  });
}
