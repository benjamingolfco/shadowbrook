import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  role: string;
  organizationId: string | null;
  isActive: boolean;
}

export function useUsers() {
  return useQuery({
    queryKey: queryKeys.users.all,
    queryFn: () => api.get<UserListItem[]>('/auth/users'),
  });
}

export function useCreateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: {
      identityId: string;
      email: string;
      displayName: string;
      role: string;
      organizationId: string | null;
    }) => api.post<UserListItem>('/auth/users', data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
    },
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...data }: {
      id: string;
      isActive?: boolean;
      role?: string;
      organizationId?: string | null;
    }) => api.put<UserListItem>(`/auth/users/${id}`, data),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.users.all });
    },
  });
}
