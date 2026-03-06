import { useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import type { VerifyCodeResponse, JoinWaitlistRequest, JoinWaitlistResponse } from '@/types/waitlist';

export function useVerifyCode() {
  return useMutation({
    mutationFn: (code: string) =>
      api.post<VerifyCodeResponse>('/walkup/verify', { code }),
  });
}

export function useJoinWaitlist() {
  return useMutation({
    mutationFn: (data: JoinWaitlistRequest) =>
      api.post<JoinWaitlistResponse>('/walkup/join', data),
  });
}
