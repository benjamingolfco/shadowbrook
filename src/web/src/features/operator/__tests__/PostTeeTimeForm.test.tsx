import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import { PostTeeTimeForm } from '../components/PostTeeTimeForm';
import { useCreateTeeTimeOpening } from '../hooks/useWaitlist';
import { useCourseContext } from '../context/CourseContext';

vi.mock('../hooks/useWaitlist');
vi.mock('../context/CourseContext');

const mockMutate = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(useCourseContext).mockReturnValue({
    course: { id: 'course-1', name: 'Pine Valley', timeZoneId: 'UTC' },
    selectCourse: vi.fn(),
    clearCourse: vi.fn(),
    isDirty: false,
    registerDirtyForm: vi.fn(),
    unregisterDirtyForm: vi.fn(),
  });
  vi.mocked(useCreateTeeTimeOpening).mockReturnValue({
    mutate: mockMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useCreateTeeTimeOpening>);
});

describe('PostTeeTimeForm', () => {
  it('renders the form with time input, slot buttons, and post button', () => {
    render(<PostTeeTimeForm courseId="course-1" />);

    expect(screen.getByLabelText('Time')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Post Tee Time' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '1' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '2' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '3' })).toBeInTheDocument();
    expect(screen.getByRole('radio', { name: '4' })).toBeInTheDocument();
  });

  it('defaults slot selection to 1', () => {
    render(<PostTeeTimeForm courseId="course-1" />);
    const slot1 = screen.getByRole('radio', { name: '1' });
    expect(slot1).toBeChecked();
  });

  it('calls create mutation with correct data on submit', async () => {
    render(<PostTeeTimeForm courseId="course-1" />);

    const timeInput = screen.getByLabelText('Time');
    fireEvent.change(timeInput, { target: { value: '10:40' } });

    const slot2 = screen.getByRole('radio', { name: '2' });
    fireEvent.click(slot2);

    fireEvent.click(screen.getByRole('button', { name: 'Post Tee Time' }));

    await waitFor(() => {
      expect(mockMutate).toHaveBeenCalledWith(
        {
          courseId: 'course-1',
          data: { teeTime: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T10:40:00$/), slotsAvailable: 2 },
        },
        expect.objectContaining({ onSuccess: expect.any(Function) }),
      );
    });
  });

  it('shows "Posting..." when mutation is pending', () => {
    vi.mocked(useCreateTeeTimeOpening).mockReturnValue({
      mutate: mockMutate,
      isPending: true,
      isError: false,
      isSuccess: false,
      error: null,
      reset: vi.fn(),
    } as unknown as ReturnType<typeof useCreateTeeTimeOpening>);

    render(<PostTeeTimeForm courseId="course-1" />);
    expect(screen.getByRole('button', { name: 'Posting...' })).toBeDisabled();
  });

  it('shows error message when mutation fails', () => {
    vi.mocked(useCreateTeeTimeOpening).mockReturnValue({
      mutate: mockMutate,
      isPending: false,
      isError: true,
      isSuccess: false,
      error: new Error('Server error'),
      reset: vi.fn(),
    } as unknown as ReturnType<typeof useCreateTeeTimeOpening>);

    render(<PostTeeTimeForm courseId="course-1" />);
    expect(screen.getByText(/couldn't post opening/i)).toBeInTheDocument();
  });
});
