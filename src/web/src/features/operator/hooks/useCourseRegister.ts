import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Course } from '@/types/course';

export interface RegisterCourseRequest {
  name: string;
  streetAddress?: string;
  city?: string;
  state?: string;
  zipCode?: string;
  contactEmail?: string;
  contactPhone?: string;
}

export function useRegisterCourse(tenantId?: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (data: RegisterCourseRequest) => {
      try {
        return await api.post<Course>('/courses', data);
      } catch (error) {
        // Handle 409 Conflict error specifically
        const errorWithStatus = error as Error & { status?: number };
        if (errorWithStatus.status === 409) {
          throw new Error('A course with this name already exists. Please choose a different name.');
        }
        throw error;
      }
    },
    onSuccess: () => {
      // Invalidate courses list to refresh with new course
      void queryClient.invalidateQueries({ queryKey: queryKeys.courses.all(tenantId) });
    },
  });
}
