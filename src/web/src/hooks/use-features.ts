import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

type FeatureFlags = Record<string, boolean>;

export function useFeatures() {
  return useQuery({
    queryKey: queryKeys.features.all,
    queryFn: () => api.get<FeatureFlags>('/api/features'),
    staleTime: 5 * 60 * 1000, // 5 minutes — flags change infrequently
  });
}

export function useFeature(key: string): boolean {
  const { data } = useFeatures();
  // Default to false while loading — safe default hides incomplete features
  return data?.[key] ?? false;
}
