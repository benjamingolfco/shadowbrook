import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { type ReactNode } from 'react';
import { useWeeklySchedule } from '../manage/hooks/useWeeklySchedule';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

vi.mock('@/lib/api-client', () => ({
  api: {
    get: vi.fn(),
  },
}));

import { api } from '@/lib/api-client';
const mockGet = vi.mocked(api.get);

describe('useWeeklySchedule', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetches weekly status for given courseId and startDate', async () => {
    const mockData = {
      weekStart: '2026-04-13',
      weekEnd: '2026-04-19',
      days: [
        { date: '2026-04-13', status: 'notStarted' },
        { date: '2026-04-14', status: 'draft', teeSheetId: 'abc', intervalCount: 72 },
      ],
    };
    mockGet.mockResolvedValueOnce(mockData);

    const { result } = renderHook(() => useWeeklySchedule('course-1', '2026-04-13'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(mockGet).toHaveBeenCalledWith('/courses/course-1/tee-sheets/week?startDate=2026-04-13');
    expect(result.current.data).toEqual(mockData);
  });
});
