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
          tenantId: 't1',
          tenantName: 'Pine Valley Golf Club',
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

    // Tenant name appears in both desktop and mobile views
    const tenantNames = screen.getAllByText('Pine Valley Golf Club');
    expect(tenantNames.length).toBe(2);

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

  it('shows tenant column as second column', () => {
    mockUseCourses.mockReturnValue({
      data: [
        {
          id: '1',
          name: 'Pine Valley',
          tenantId: 't1',
          tenantName: 'Pine Valley Golf Club',
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

    const headers = screen.getAllByRole('columnheader');
    expect(headers[0]).toHaveTextContent('Name');
    expect(headers[1]).toHaveTextContent('Tenant');
    expect(headers[2]).toHaveTextContent('Location');
    expect(headers[3]).toHaveTextContent('Contact');
    expect(headers[4]).toHaveTextContent('Registered');
  });

  it('displays em dash when tenant name is missing', () => {
    mockUseCourses.mockReturnValue({
      data: [
        {
          id: '1',
          name: 'Pine Valley',
          tenantId: 't1',
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

    const { container } = render(<CourseList />);

    // The em dash should appear for missing tenant name in both desktop and mobile views
    expect(container.textContent).toContain('—');
  });

  it('shows tenant below course name on mobile', () => {
    mockUseCourses.mockReturnValue({
      data: [
        {
          id: '1',
          name: 'Pine Valley',
          tenantId: 't1',
          tenantName: 'Pine Valley Golf Club',
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

    // Verify tenant name appears in both desktop (hidden md:table-cell) and mobile (md:hidden) views
    const tenantNames = screen.getAllByText('Pine Valley Golf Club');
    expect(tenantNames.length).toBe(2); // One for desktop column, one for mobile below name
  });
});
