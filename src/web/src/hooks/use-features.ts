import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

type FeatureFlags = Record<string, boolean>;

export function useFeatures(courseId?: string) {
  const url = courseId ? `/features?courseId=${courseId}` : '/features';
  return useQuery({
    queryKey: courseId ? queryKeys.features.byCourse(courseId) : queryKeys.features.all,
    queryFn: () => api.get<FeatureFlags>(url),
    staleTime: 5 * 60 * 1000, // 5 minutes — flags change infrequently
  });
}

export function useFeature(key: string, courseId?: string): boolean {
  const { data } = useFeatures(courseId);
  // Default to false while loading — safe default hides incomplete features
  return data?.[key] ?? false;
}
