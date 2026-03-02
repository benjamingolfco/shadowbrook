import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import Waitlist from '../pages/Waitlist';
import { useCourseContext } from '../context/CourseContext';
import {
  useWaitlistSettings,
  useWaitlist,
  useCreateWaitlistRequest,
} from '../hooks/useWaitlist';

vi.mock('../context/CourseContext');
vi.mock('../hooks/useWaitlist');

const mockUseCourseContext = vi.mocked(useCourseContext);
const mockUseWaitlistSettings = vi.mocked(useWaitlistSettings);
const mockUseWaitlist = vi.mocked(useWaitlist);
const mockUseCreateWaitlistRequest = vi.mocked(useCreateWaitlistRequest);

const mockCourse = { id: 'course-1', name: 'Pine Valley' };

const defaultMutationReturn = {
  mutate: vi.fn(),
  isPending: false,
  isSuccess: false,
  isError: false,
  error: null,
} as unknown as ReturnType<typeof useCreateWaitlistRequest>;

const enabledSettingsReturn = {
  data: { waitlistEnabled: true },
  isLoading: false,
  isError: false,
  error: null,
} as unknown as ReturnType<typeof useWaitlistSettings>;

const disabledSettingsReturn = {
  data: { waitlistEnabled: false },
  isLoading: false,
  isError: false,
  error: null,
} as unknown as ReturnType<typeof useWaitlistSettings>;

const emptyWaitlistReturn = {
  data: { courseWaitlistId: null, date: '2026-03-02', totalGolfersPending: 0, requests: [] },
  isLoading: false,
  isError: false,
  error: null,
} as unknown as ReturnType<typeof useWaitlist>;

beforeEach(() => {
  vi.clearAllMocks();

  mockUseCourseContext.mockReturnValue({
    course: mockCourse,
    selectCourse: vi.fn(),
    clearCourse: vi.fn(),
    isDirty: false,
    registerDirtyForm: vi.fn(),
    unregisterDirtyForm: vi.fn(),
  });

  mockUseWaitlistSettings.mockReturnValue(enabledSettingsReturn);
  mockUseWaitlist.mockReturnValue(emptyWaitlistReturn);
  mockUseCreateWaitlistRequest.mockReturnValue(defaultMutationReturn);
});

describe('Waitlist', () => {
  it('shows "Select a course" when no course selected', () => {
    mockUseCourseContext.mockReturnValue({
      course: null,
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
      isDirty: false,
      registerDirtyForm: vi.fn(),
      unregisterDirtyForm: vi.fn(),
    });

    render(<Waitlist />);

    expect(
      screen.getByText('Select a course from the sidebar to manage the waitlist.'),
    ).toBeInTheDocument();
  });

  it('shows feature disabled callout when waitlist is not enabled', () => {
    mockUseWaitlistSettings.mockReturnValue(disabledSettingsReturn);

    render(<Waitlist />);

    expect(
      screen.getByText('Waitlist is not enabled for this course.'),
    ).toBeInTheDocument();
    expect(
      screen.getByText('Enable the waitlist in course settings to use this feature.'),
    ).toBeInTheDocument();
  });

  it('shows loading skeletons while loading', () => {
    mockUseWaitlistSettings.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useWaitlistSettings>);
    mockUseWaitlist.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useWaitlist>);

    render(<Waitlist />);

    const skeletons = document.querySelectorAll('[data-slot="skeleton"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows summary card with total golfers pending', () => {
    mockUseWaitlist.mockReturnValue({
      data: {
        courseWaitlistId: 'wl-1',
        date: '2026-03-02',
        totalGolfersPending: 7,
        requests: [],
      },
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useWaitlist>);

    render(<Waitlist />);

    expect(screen.getByText('Total Golfers Pending')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('shows entries table with pending badges', () => {
    mockUseWaitlist.mockReturnValue({
      data: {
        courseWaitlistId: 'wl-1',
        date: '2026-03-02',
        totalGolfersPending: 5,
        requests: [
          { id: 'req-1', teeTime: '08:00', golfersNeeded: 2, status: 'Pending' },
          { id: 'req-2', teeTime: '09:30', golfersNeeded: 3, status: 'Pending' },
        ],
      },
      isLoading: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useWaitlist>);

    render(<Waitlist />);

    expect(screen.getByText('8:00 AM')).toBeInTheDocument();
    expect(screen.getByText('9:30 AM')).toBeInTheDocument();
    expect(screen.getByText('2 pending')).toBeInTheDocument();
    expect(screen.getByText('3 pending')).toBeInTheDocument();
  });

  it('shows empty state when no entries', () => {
    render(<Waitlist />);

    expect(screen.getByText('No waitlist entries for this date.')).toBeInTheDocument();
  });

  it('shows error text when fetch fails', () => {
    mockUseWaitlist.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useWaitlist>);

    render(<Waitlist />);

    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('submit button is disabled during mutation', () => {
    mockUseCreateWaitlistRequest.mockReturnValue({
      mutate: vi.fn(),
      isPending: true,
      isSuccess: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useCreateWaitlistRequest>);

    render(<Waitlist />);

    const button = screen.getByRole('button', { name: 'Adding...' });
    expect(button).toBeDisabled();
  });

  it('shows success message after successful submission', () => {
    mockUseCreateWaitlistRequest.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isSuccess: true,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useCreateWaitlistRequest>);

    render(<Waitlist />);

    const successMsg = screen.getByRole('status');
    expect(successMsg).toHaveTextContent('Tee time added to waitlist.');
  });

  it('shows error message after failed submission', () => {
    mockUseCreateWaitlistRequest.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
      isSuccess: false,
      isError: true,
      error: new Error('An active waitlist request already exists for this tee time.'),
    } as unknown as ReturnType<typeof useCreateWaitlistRequest>);

    render(<Waitlist />);

    const errorMsg = screen.getByRole('alert');
    expect(errorMsg).toHaveTextContent(
      'An active waitlist request already exists for this tee time.',
    );
  });
});
