import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Course } from '@/types/course';

export function useCourses(tenantId: string | undefined) {
  return useQuery({
    queryKey: tenantId ? queryKeys.courses.all(tenantId) : ['courses', 'disabled'],
    queryFn: () => api.get<Course[]>('/courses'),
    enabled: !!tenantId,
  });
}
