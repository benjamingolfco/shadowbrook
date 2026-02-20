import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import type { Course } from '@/types/course';

export function useCourses() {
  return useQuery({
    queryKey: ['courses', 'admin'],
    queryFn: () => api.get<Course[]>('/courses'),
  });
}
