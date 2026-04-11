import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { type ReactNode } from 'react';
import { CourseProvider, useCourseContext } from '../context/CourseProvider';

vi.mock('@/lib/api-client', () => ({
  api: {
    get: vi.fn(),
  },
}));

import { api } from '@/lib/api-client';
const mockGet = vi.mocked(api.get);

const mockCourse = {
  id: 'course-123',
  name: 'Pebble Beach',
  tenantId: 'tenant-1',
  timeZoneId: 'America/Los_Angeles',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
};

function createWrapper(initialRoute = '/courses/course-123') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[initialRoute]}>
          <Routes>
            <Route
              path="/courses/:courseId"
              element={<CourseProvider>{children}</CourseProvider>}
            />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
  };
}

function CourseDisplay() {
  const { course, isLoading } = useCourseContext();
  if (isLoading) return <div>Loading...</div>;
  if (!course) return <div>No course</div>;
  return (
    <div>
      <span data-testid="course-name">{course.name}</span>
      <span data-testid="course-timezone">{course.timeZoneId}</span>
    </div>
  );
}

describe('CourseProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows isLoading true while fetching', () => {
    mockGet.mockReturnValue(new Promise(() => {}));

    const Wrapper = createWrapper();
    render(<CourseDisplay />, { wrapper: Wrapper });

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });

  it('provides course name and timeZoneId after fetch resolves', async () => {
    mockGet.mockResolvedValueOnce(mockCourse);

    const Wrapper = createWrapper();
    render(<CourseDisplay />, { wrapper: Wrapper });

    await waitFor(() => {
      expect(screen.getByTestId('course-name')).toHaveTextContent('Pebble Beach');
    });

    expect(screen.getByTestId('course-timezone')).toHaveTextContent('America/Los_Angeles');
    expect(mockGet).toHaveBeenCalledWith('/courses/course-123');
  });

  it('exposes null course before data loads', () => {
    mockGet.mockReturnValue(new Promise(() => {}));

    const Wrapper = createWrapper();
    render(<CourseDisplay />, { wrapper: Wrapper });

    expect(screen.getByText('Loading...')).toBeInTheDocument();
    expect(screen.queryByTestId('course-name')).not.toBeInTheDocument();
  });
});
