import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@/test/test-utils';
import CoursePortfolio from '../pages/CoursePortfolio';
import { useCourses } from '@/features/operator/hooks/useCourses';
import { useCourseContext } from '../context/CourseContext';

vi.mock('@/features/operator/hooks/useCourses');
vi.mock('../context/CourseContext');


const mockUseCourses = vi.mocked(useCourses);
const mockUseCourseContext = vi.mocked(useCourseContext);

const mockSelectCourse = vi.fn();
const mockRefetch = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();

  mockUseCourseContext.mockReturnValue({
    course: null,
    selectCourse: mockSelectCourse,
    clearCourse: vi.fn(),
    isDirty: false,
    registerDirtyForm: vi.fn(),
    unregisterDirtyForm: vi.fn(),
  });
});

describe('CoursePortfolio', () => {
  it('shows skeleton cards in loading state', () => {
    mockUseCourses.mockReturnValue({
      isLoading: true,
      data: undefined,
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(screen.getByText('Select a Course')).toBeInTheDocument();
    expect(screen.getByLabelText('Loading courses')).toHaveAttribute('aria-busy', 'true');
  });

  it('shows error message and retry button on error', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: undefined,
      error: new Error('Network error'),
      isError: true,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(screen.getByText('Select a Course')).toBeInTheDocument();
    expect(screen.getByText(/Error loading courses: Network error/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('shows admin contact message when no courses registered', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(screen.getByText('No courses available')).toBeInTheDocument();
    expect(screen.getByText('Contact your administrator to add a course.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Register a Course' })).not.toBeInTheDocument();
  });

  it('auto-selects single course', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [
        {
          id: 'course-1',
          name: 'Spyglass Hill',
          timeZoneId: 'America/Los_Angeles',
          city: 'Pebble Beach',
          state: 'CA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      ],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(mockSelectCourse).toHaveBeenCalledWith({ id: 'course-1', name: 'Spyglass Hill', timeZoneId: 'America/Los_Angeles' });
  });

  it('renders multiple course cards', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [
        {
          id: 'course-1',
          name: 'Pine Valley',
          city: 'Augusta',
          state: 'GA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        {
          id: 'course-2',
          name: 'Spyglass Hill',
          city: 'Pebble Beach',
          state: 'CA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      ],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(screen.getByText('Pine Valley')).toBeInTheDocument();
    expect(screen.getByText('Spyglass Hill')).toBeInTheDocument();
    expect(screen.getByText('Augusta, GA')).toBeInTheDocument();
    expect(screen.getByText('Pebble Beach, CA')).toBeInTheDocument();
    // "Manage" buttons are aria-hidden (decorative), use getAllByText with hidden:true
    expect(screen.getAllByText('Manage', { selector: 'button' })).toHaveLength(2);
  });

  it('calls selectCourse when a course card is clicked', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [
        {
          id: 'course-1',
          name: 'Pine Valley',
          timeZoneId: 'America/New_York',
          city: 'Augusta',
          state: 'GA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        {
          id: 'course-2',
          name: 'Spyglass Hill',
          timeZoneId: 'America/Los_Angeles',
          city: 'Pebble Beach',
          state: 'CA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      ],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    act(() => {
      screen.getByLabelText('Manage Pine Valley, Augusta, GA').click();
    });

    expect(mockSelectCourse).toHaveBeenCalledWith({ id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/New_York' });
  });

  it('calls selectCourse on Enter keydown on a course card', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [
        {
          id: 'course-1',
          name: 'Pine Valley',
          timeZoneId: 'America/New_York',
          city: 'Augusta',
          state: 'GA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      ],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    const card = screen.getByLabelText('Manage Pine Valley, Augusta, GA');
    act(() => {
      card.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    });

    expect(mockSelectCourse).toHaveBeenCalledWith({ id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/New_York' });
  });

  it('calls selectCourse on Space keydown on a course card', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [
        {
          id: 'course-1',
          name: 'Pine Valley',
          timeZoneId: 'America/New_York',
          city: 'Augusta',
          state: 'GA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      ],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    const card = screen.getByLabelText('Manage Pine Valley, Augusta, GA');
    act(() => {
      card.dispatchEvent(new KeyboardEvent('keydown', { key: ' ', bubbles: true }));
    });

    expect(mockSelectCourse).toHaveBeenCalledWith({ id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/New_York' });
  });

  it('cards have correct aria-labels including location', () => {
    mockUseCourses.mockReturnValue({
      isLoading: false,
      data: [
        {
          id: 'course-1',
          name: 'Pine Valley',
          city: 'Augusta',
          state: 'GA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
        {
          id: 'course-2',
          name: 'Spyglass Hill',
          city: 'Pebble Beach',
          state: 'CA',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        },
      ],
      error: null,
      isError: false,
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useCourses>);

    render(<CoursePortfolio />);

    expect(screen.getByLabelText('Manage Pine Valley, Augusta, GA')).toBeInTheDocument();
    expect(screen.getByLabelText('Manage Spyglass Hill, Pebble Beach, CA')).toBeInTheDocument();
  });

});
