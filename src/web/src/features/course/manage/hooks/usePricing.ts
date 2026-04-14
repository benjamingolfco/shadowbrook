import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Pricing, RateSchedule } from '@/types/course';

export function usePricing(courseId: string | undefined) {
  return useQuery({
    queryKey: courseId ? queryKeys.courses.pricing(courseId) : ['disabled'],
    queryFn: () => api.get<Pricing>(`/courses/${courseId}/pricing`),
    enabled: !!courseId,
  });
}

export function useUpdateDefaultPrice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, defaultPrice }: { courseId: string; defaultPrice: number }) =>
      api.put<{ defaultPrice: number }>(`/courses/${courseId}/pricing/default`, { defaultPrice }),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.pricing(courseId) });
    },
  });
}

export function useUpdateBounds() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, minPrice, maxPrice }: { courseId: string; minPrice: number; maxPrice: number }) =>
      api.put<{ minPrice: number; maxPrice: number }>(`/courses/${courseId}/pricing/bounds`, { minPrice, maxPrice }),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.pricing(courseId) });
    },
  });
}

export function useCreateSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, data }: { courseId: string; data: Omit<RateSchedule, 'id' | 'invalidReason'> }) =>
      api.post<RateSchedule>(`/courses/${courseId}/pricing/schedules`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.pricing(courseId) });
    },
  });
}

export function useUpdateSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, scheduleId, data }: { courseId: string; scheduleId: string; data: Omit<RateSchedule, 'id' | 'invalidReason'> }) =>
      api.put<RateSchedule>(`/courses/${courseId}/pricing/schedules/${scheduleId}`, data),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.pricing(courseId) });
    },
  });
}

export function useDeleteSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ courseId, scheduleId }: { courseId: string; scheduleId: string }) =>
      api.delete(`/courses/${courseId}/pricing/schedules/${scheduleId}`),
    onSuccess: (_, { courseId }) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.pricing(courseId) });
    },
  });
}
