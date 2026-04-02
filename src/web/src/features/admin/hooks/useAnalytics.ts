import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

interface PlatformSummary {
  totalOrganizations: number;
  totalCourses: number;
  activeUsers: number;
  bookingsToday: number;
}

interface FillRateResult {
  date: string;
  totalSlots: number;
  filledSlots: number;
  fillPercentage: number;
}

interface BookingTrendResult {
  date: string;
  bookingCount: number;
}

interface PopularTimeResult {
  time: string;
  bookingCount: number;
}

interface WaitlistStatsResult {
  activeEntries: number;
  offersSent: number;
  offersAccepted: number;
  offersRejected: number;
}

export function useSummary() {
  return useQuery({
    queryKey: queryKeys.analytics.summary,
    queryFn: () => api.get<PlatformSummary>('/admin/analytics/summary'),
  });
}

export function useFillRates(courseId?: string, days = 7) {
  const params = new URLSearchParams();
  if (courseId) params.set('courseId', courseId);
  params.set('days', String(days));
  const qs = params.toString();

  return useQuery({
    queryKey: queryKeys.analytics.fillRates(courseId),
    queryFn: () => api.get<FillRateResult[]>(`/admin/analytics/fill-rates?${qs}`),
  });
}

export function useBookingTrends(courseId?: string, days = 30) {
  const params = new URLSearchParams();
  if (courseId) params.set('courseId', courseId);
  params.set('days', String(days));
  const qs = params.toString();

  return useQuery({
    queryKey: queryKeys.analytics.bookings(courseId),
    queryFn: () => api.get<BookingTrendResult[]>(`/admin/analytics/bookings?${qs}`),
  });
}

export function usePopularTimes(courseId?: string, days = 30) {
  const params = new URLSearchParams();
  if (courseId) params.set('courseId', courseId);
  params.set('days', String(days));
  const qs = params.toString();

  return useQuery({
    queryKey: queryKeys.analytics.popularTimes(courseId),
    queryFn: () => api.get<PopularTimeResult[]>(`/admin/analytics/popular-times?${qs}`),
  });
}

export function useWaitlistStats(courseId?: string) {
  const qs = courseId ? `?courseId=${courseId}` : '';

  return useQuery({
    queryKey: queryKeys.analytics.waitlist(courseId),
    queryFn: () => api.get<WaitlistStatsResult>(`/admin/analytics/waitlist${qs}`),
  });
}
