import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@/test/test-utils';
import CourseList from '../pages/CourseList';

vi.mock('../hooks/useCourses');

import { useCourses } from '../hooks/useCourses';

const mockUseCourses = vi.mocked(useCourses);

describe('CourseList', () => {
  it('shows loading state', () => {
    mockUseCourses.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CourseList />);
    expect(screen.getByText('Loading courses...')).toBeInTheDocument();
  });

  it('shows error state', () => {
    mockUseCourses.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useCourses>);

    render(<CourseList />);
    expect(screen.getByText('Error: Network error')).toBeInTheDocument();
  });

  it('shows empty state', () => {
    mockUseCourses.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CourseList />);
    expect(screen.getByText('No courses registered yet.')).toBeInTheDocument();
  });

  it('renders course data', () => {
    mockUseCourses.mockReturnValue({
      data: [
        {
          id: '1',
          name: 'Pine Valley',
          city: 'Clementon',
          state: 'NJ',
          contactEmail: 'pro@pinevalley.com',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CourseList />);
    expect(screen.getByText('Pine Valley')).toBeInTheDocument();
    expect(screen.getByText('pro@pinevalley.com')).toBeInTheDocument();
    expect(screen.getByText(/Clementon/)).toBeInTheDocument();
  });

  it('shows register course link', () => {
    mockUseCourses.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CourseList />);
    expect(screen.getByRole('link', { name: 'Register Course' })).toHaveAttribute(
      'href',
      '/admin/courses/new',
    );
  });
});
