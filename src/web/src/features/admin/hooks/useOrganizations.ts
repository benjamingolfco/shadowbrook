import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';
import type { Organization } from '@/types/organization';

export function useOrganizations() {
  return useQuery({
    queryKey: queryKeys.organizations.all,
    queryFn: () => api.get<Organization[]>('/organizations'),
  });
}

export function useOrganization(id: string) {
  return useQuery({
    queryKey: queryKeys.organizations.detail(id),
    queryFn: () => api.get<OrganizationDetail>(`/organizations/${id}`),
    enabled: !!id,
  });
}

export function useCreateOrganization() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: { name: string; operatorEmail: string }) => api.post<{ id: string; name: string }>('/organizations', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
    },
  });
}

export function useUpdateOrganization() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: { id: string; name: string }) =>
      api.put<{ id: string; name: string }>(`/organizations/${id}`, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.organizations.all });
    },
  });
}

interface OrganizationDetail {
  id: string;
  name: string;
  createdAt: string;
  courses: { id: string; name: string }[];
  users: { id: string; email: string; firstName: string | null; lastName: string | null; role: string; isActive: boolean }[];
}
