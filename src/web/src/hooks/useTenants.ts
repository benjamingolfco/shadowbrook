import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Tenant } from '@/types/tenant';

export function useTenants() {
  return useQuery({
    queryKey: queryKeys.tenants.all,
    queryFn: () => api.get<Tenant[]>('/tenants'),
  });
}
