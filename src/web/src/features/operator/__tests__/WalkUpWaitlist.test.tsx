import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import WalkUpWaitlist from '../pages/WalkUpWaitlist';
import { useCourseContext } from '../context/CourseContext';
import {
  useWalkUpWaitlistToday,
  useOpenWalkUpWaitlist,
  useCloseWalkUpWaitlist,
} from '../hooks/useWalkUpWaitlist';

vi.mock('../context/CourseContext');
vi.mock('../hooks/useWalkUpWaitlist');

const mockUseCourseContext = vi.mocked(useCourseContext);
const mockUseWalkUpWaitlistToday = vi.mocked(useWalkUpWaitlistToday);
const mockUseOpenWalkUpWaitlist = vi.mocked(useOpenWalkUpWaitlist);
const mockUseCloseWalkUpWaitlist = vi.mocked(useCloseWalkUpWaitlist);

const mockCourse = { id: 'course-1', name: 'Pine Valley' };

const openWaitlist = {
  id: 'wl-1',
  courseId: 'course-1',
  shortCode: '4827',
  date: '2026-03-05',
  status: 'Open' as const,
  openedAt: '2026-03-05T09:00:00Z',
  closedAt: null,
};

const closedWaitlist = {
  ...openWaitlist,
  status: 'Closed' as const,
  closedAt: '2026-03-05T17:00:00Z',
};

const mockEntries = [
  { id: 'e-1', golferName: 'Alice Smith', joinedAt: '2026-03-05T09:15:00Z' },
  { id: 'e-2', golferName: 'Bob Jones', joinedAt: '2026-03-05T09:20:00Z' },
];

const mockOpenMutate = vi.fn();
const mockCloseMutate = vi.fn();

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

function defaultOpenMutation(overrides = {}) {
  mockUseOpenWalkUpWaitlist.mockReturnValue({
    mutate: mockOpenMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useOpenWalkUpWaitlist>);
}

function defaultCloseMutation(overrides = {}) {
  mockUseCloseWalkUpWaitlist.mockReturnValue({
    mutate: mockCloseMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useCloseWalkUpWaitlist>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultCourseContext();
  defaultOpenMutation();
  defaultCloseMutation();
});

describe('WalkUpWaitlist', () => {
  it('renders loading state', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(document.querySelector('[data-slot="skeleton"]')).toBeInTheDocument();
  });

  it('renders inactive state with open button when no waitlist exists today', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: null, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByRole('button', { name: 'Open Waitlist' })).toBeInTheDocument();
  });

  it('renders open state with short code displayed', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Short code displayed with spaces between digits
    expect(screen.getByText('4 8 2 7')).toBeInTheDocument();
  });

  it('renders closed state with Closed badge', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: closedWaitlist, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByText('Closed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Close Waitlist' })).not.toBeInTheDocument();
  });

  it('calls open mutation when Open Waitlist button is clicked', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: null, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    fireEvent.click(screen.getByRole('button', { name: 'Open Waitlist' }));

    expect(mockOpenMutate).toHaveBeenCalledWith({ courseId: 'course-1' });
  });

  it('shows close confirmation dialog when Close Waitlist button is clicked', async () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    fireEvent.click(screen.getByRole('button', { name: 'Close Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('Close Walk-Up Waitlist?')).toBeInTheDocument();
    });

    expect(
      screen.getByText('No new golfers will be able to join. Existing entries will be preserved.')
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Keep Open' })).toBeInTheDocument();
  });

  it('calls close mutation when confirmed in AlertDialog', async () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Open the dialog via the trigger button
    fireEvent.click(screen.getByRole('button', { name: 'Close Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('Close Walk-Up Waitlist?')).toBeInTheDocument();
    });

    // The confirm action inside the AlertDialog has data-slot="alert-dialog-action"
    const confirmButton = document.querySelector('[data-slot="alert-dialog-action"]');
    expect(confirmButton).toBeInTheDocument();
    fireEvent.click(confirmButton!);

    expect(mockCloseMutate).toHaveBeenCalledWith({ courseId: 'course-1' });
  });

  it('renders empty queue message when waitlist is open with no entries', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(
      screen.getByText('No golfers have joined yet. Share the short code with walk-up golfers.')
    ).toBeInTheDocument();
  });

  it('renders golfer entries in the queue table', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: mockEntries },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getAllByText('Alice Smith').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Bob Jones').length).toBeGreaterThan(0);
  });

  it('renders error state with retry button when query fails', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
      error: new Error('Failed to fetch'),
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByText(/Error loading waitlist: Failed to fetch/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('shows 409 error message when waitlist is already open', () => {
    const error = Object.assign(new Error('Waitlist is already open for today.'), { status: 409 });

    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: null, entries: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    mockUseOpenWalkUpWaitlist.mockReturnValue({
      mutate: mockOpenMutate,
      isPending: false,
      isError: true,
      isSuccess: false,
      error,
    } as unknown as ReturnType<typeof useOpenWalkUpWaitlist>);

    render(<WalkUpWaitlist />);

    expect(screen.getByText('Waitlist is already open for today.')).toBeInTheDocument();
  });
});
