import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Tenant, TenantDetail } from '@/types/tenant';

export function useTenants() {
  return useQuery({
    queryKey: queryKeys.tenants.all,
    queryFn: () => api.get<Tenant[]>('/tenants'),
  });
}

export function useTenant(id: string) {
  return useQuery({
    queryKey: queryKeys.tenants.detail(id),
    queryFn: () => api.get<TenantDetail>(`/tenants/${id}`),
    enabled: !!id,
  });
}
