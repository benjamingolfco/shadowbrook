import { useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api-client';

interface JoinWaitlistRequest {
  courseWaitlistId: string;
  firstName: string;
  lastName: string;
  phone: string;
}

interface JoinWaitlistResponse {
  entryId: string;
  firstName: string;
  position: number;
  isExisting: boolean;
}

export function useJoinWaitlist() {
  return useMutation({
    mutationFn: (data: JoinWaitlistRequest) =>
      api.post<JoinWaitlistResponse>('/walkup/join', data),
  });
}
