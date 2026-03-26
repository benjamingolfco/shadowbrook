import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import { AddTeeTimeRequestDialog } from '../components/AddTeeTimeRequestDialog';
import { useCourseContext } from '../context/CourseContext';
import { useCreateWaitlistOpening } from '../hooks/useWaitlist';
import * as courseTime from '@/lib/course-time';

vi.mock('../context/CourseContext');
vi.mock('../hooks/useWaitlist');

const mockUseCourseContext = vi.mocked(useCourseContext);
const mockUseCreateWaitlistOpening = vi.mocked(useCreateWaitlistOpening);

const mockCourse = { id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/Chicago' };

const mockCreateMutate = vi.fn();

function defaultCourseContext() {
  mockUseCourseContext.mockReturnValue({
    course: mockCourse,
    selectCourse: vi.fn(),
    clearCourse: vi.fn(),
    isDirty: false,
    registerDirtyForm: vi.fn(),
    unregisterDirtyForm: vi.fn(),
  });
}

function defaultCreateWaitlistOpening() {
  mockUseCreateWaitlistOpening.mockReturnValue({
    mutate: mockCreateMutate,
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useCreateWaitlistOpening>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultCourseContext();
  defaultCreateWaitlistOpening();
});

describe('AddTeeTimeRequestDialog', () => {
  it('shows validation error when tee time is in the past', async () => {
    // Mock getCourseNow to return 14:00 (2:00 PM)
    vi.spyOn(courseTime, 'getCourseNow').mockReturnValue('14:00');

    render(
      <AddTeeTimeRequestDialog
        open={true}
        onOpenChange={vi.fn()}
        courseId="course-1"
      />
    );

    // Enter a past tee time (13:00, which is 1 hour before current time)
    const teeTimeInput = screen.getByLabelText('Tee Time');
    fireEvent.change(teeTimeInput, { target: { value: '13:00' } });

    // Submit the form to trigger validation
    const submitButton = screen.getByRole('button', { name: 'Add Opening' });
    fireEvent.click(submitButton);

    // Wait for validation error to appear
    await waitFor(() => {
      expect(screen.getByText('Tee time must be in the future')).toBeInTheDocument();
    });
  });

  it('does not show validation error when tee time is in the future', async () => {
    // Mock getCourseNow to return 14:00 (2:00 PM)
    vi.spyOn(courseTime, 'getCourseNow').mockReturnValue('14:00');

    render(
      <AddTeeTimeRequestDialog
        open={true}
        onOpenChange={vi.fn()}
        courseId="course-1"
      />
    );

    // Enter a future tee time (15:00, which is 1 hour after current time)
    const teeTimeInput = screen.getByLabelText('Tee Time');
    fireEvent.change(teeTimeInput, { target: { value: '15:00' } });

    // Submit the form to trigger validation
    const submitButton = screen.getByRole('button', { name: 'Add Opening' });
    fireEvent.click(submitButton);

    // Wait a moment to ensure validation runs
    await waitFor(() => {
      expect(screen.queryByText('Tee time must be in the future')).not.toBeInTheDocument();
    });
  });

  it('allows tee time within 5-minute grace period', async () => {
    // Mock getCourseNow to return 14:00 (2:00 PM)
    vi.spyOn(courseTime, 'getCourseNow').mockReturnValue('14:00');

    render(
      <AddTeeTimeRequestDialog
        open={true}
        onOpenChange={vi.fn()}
        courseId="course-1"
      />
    );

    // Enter a tee time 3 minutes in the past (within grace period)
    const teeTimeInput = screen.getByLabelText('Tee Time');
    fireEvent.change(teeTimeInput, { target: { value: '13:57' } });

    // Submit the form to trigger validation
    const submitButton = screen.getByRole('button', { name: 'Add Opening' });
    fireEvent.click(submitButton);

    // Wait a moment to ensure validation runs
    await waitFor(() => {
      expect(screen.queryByText('Tee time must be in the future')).not.toBeInTheDocument();
    });
  });

  it('shows validation error for tee time just outside grace period', async () => {
    // Mock getCourseNow to return 14:00 (2:00 PM)
    vi.spyOn(courseTime, 'getCourseNow').mockReturnValue('14:00');

    render(
      <AddTeeTimeRequestDialog
        open={true}
        onOpenChange={vi.fn()}
        courseId="course-1"
      />
    );

    // Enter a tee time 6 minutes in the past (outside grace period)
    const teeTimeInput = screen.getByLabelText('Tee Time');
    fireEvent.change(teeTimeInput, { target: { value: '13:54' } });

    // Submit the form to trigger validation
    const submitButton = screen.getByRole('button', { name: 'Add Opening' });
    fireEvent.click(submitButton);

    // Wait for validation error to appear
    await waitFor(() => {
      expect(screen.getByText('Tee time must be in the future')).toBeInTheDocument();
    });
  });

  it('clears error when user changes to a valid time', async () => {
    // Mock getCourseNow to return 14:00 (2:00 PM)
    vi.spyOn(courseTime, 'getCourseNow').mockReturnValue('14:00');

    render(
      <AddTeeTimeRequestDialog
        open={true}
        onOpenChange={vi.fn()}
        courseId="course-1"
      />
    );

    const teeTimeInput = screen.getByLabelText('Tee Time');
    const submitButton = screen.getByRole('button', { name: 'Add Opening' });

    // Enter a past tee time and submit
    fireEvent.change(teeTimeInput, { target: { value: '13:00' } });
    fireEvent.click(submitButton);

    // Wait for validation error to appear
    await waitFor(() => {
      expect(screen.getByText('Tee time must be in the future')).toBeInTheDocument();
    });

    // Change to a future tee time and submit again
    fireEvent.change(teeTimeInput, { target: { value: '15:00' } });
    fireEvent.click(submitButton);

    // Wait for error to clear
    await waitFor(() => {
      expect(screen.queryByText('Tee time must be in the future')).not.toBeInTheDocument();
    });
  });

  it('shows required error when tee time is empty', async () => {
    vi.spyOn(courseTime, 'getCourseNow').mockReturnValue('14:00');

    render(
      <AddTeeTimeRequestDialog
        open={true}
        onOpenChange={vi.fn()}
        courseId="course-1"
      />
    );

    // Try to submit without entering tee time
    const submitButton = screen.getByRole('button', { name: 'Add Opening' });
    fireEvent.click(submitButton);

    // Wait for required error to appear
    await waitFor(() => {
      expect(screen.getByText('Tee time is required')).toBeInTheDocument();
    });
  });
});
