import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Course } from '@/types/course';

export function useCourses() {
  return useQuery({
    queryKey: queryKeys.courses.all(),
    queryFn: () => api.get<Course[]>('/courses'),
  });
}
