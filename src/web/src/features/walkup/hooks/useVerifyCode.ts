import { useMutation } from '@tanstack/react-query';
import { api } from '@/lib/api-client';

interface VerifyCodeResponse {
  courseWaitlistId: string;
  courseId: string;
  courseName: string;
  date: string;
}

export function useVerifyCode() {
  return useMutation({
    mutationFn: (code: string) =>
      api.post<VerifyCodeResponse>('/walkup/verify', { code }),
  });
}
