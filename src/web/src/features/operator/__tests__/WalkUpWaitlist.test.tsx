import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import WalkUpWaitlist from '../pages/WalkUpWaitlist';
import { useCourseContext } from '../context/CourseContext';
import {
  useWalkUpWaitlistToday,
  useOpenWalkUpWaitlist,
  useCloseWalkUpWaitlist,
  useReopenWalkUpWaitlist,
} from '../hooks/useWalkUpWaitlist';
import {
  useAddGolferToWaitlist,
  useCreateTeeTimeOpening,
  useRemoveGolferFromWaitlist,
  useCancelTeeTimeOpening,
} from '../hooks/useWaitlist';

vi.mock('../context/CourseContext');
vi.mock('../hooks/useWalkUpWaitlist');
vi.mock('../hooks/useWaitlist');
vi.mock('qrcode.react', () => ({
  QRCodeCanvas: ({ value }: { value: string }) => (
    <canvas data-testid="qr-canvas" data-value={value} />
  ),
}));

const mockUseCourseContext = vi.mocked(useCourseContext);
const mockUseWalkUpWaitlistToday = vi.mocked(useWalkUpWaitlistToday);
const mockUseOpenWalkUpWaitlist = vi.mocked(useOpenWalkUpWaitlist);
const mockUseCloseWalkUpWaitlist = vi.mocked(useCloseWalkUpWaitlist);
const mockUseReopenWalkUpWaitlist = vi.mocked(useReopenWalkUpWaitlist);
const mockUseAddGolferToWaitlist = vi.mocked(useAddGolferToWaitlist);
const mockUseCreateTeeTimeOpening = vi.mocked(useCreateTeeTimeOpening);
const mockUseRemoveGolferFromWaitlist = vi.mocked(useRemoveGolferFromWaitlist);
const mockUseCancelTeeTimeOpening = vi.mocked(useCancelTeeTimeOpening);

const mockCourse = { id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/Chicago' };

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
  { id: 'e-1', golferName: 'Alice Smith', groupSize: 2, joinedAt: '2026-03-05T09:15:00Z' },
  { id: 'e-2', golferName: 'Bob Jones', groupSize: 1, joinedAt: '2026-03-05T09:20:00Z' },
];

const mockOpenMutate = vi.fn();
const mockCloseMutate = vi.fn();
const mockReopenMutate = vi.fn();

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

function defaultReopenMutation(overrides = {}) {
  mockUseReopenWalkUpWaitlist.mockReturnValue({
    mutate: mockReopenMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useReopenWalkUpWaitlist>);
}

function defaultAddGolferToWaitlist() {
  mockUseAddGolferToWaitlist.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useAddGolferToWaitlist>);
}

function defaultCreateTeeTimeOpening() {
  mockUseCreateTeeTimeOpening.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useCreateTeeTimeOpening>);
}

function defaultRemoveGolferFromWaitlist() {
  mockUseRemoveGolferFromWaitlist.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useRemoveGolferFromWaitlist>);
}

function defaultCancelTeeTimeOpening() {
  mockUseCancelTeeTimeOpening.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isSuccess: false,
    isError: false,
    error: null,
    reset: vi.fn(),
  } as unknown as ReturnType<typeof useCancelTeeTimeOpening>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultCourseContext();
  defaultOpenMutation();
  defaultCloseMutation();
  defaultReopenMutation();
  defaultAddGolferToWaitlist();
  defaultCreateTeeTimeOpening();
  defaultRemoveGolferFromWaitlist();
  defaultCancelTeeTimeOpening();
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
      data: { waitlist: null, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByRole('button', { name: 'Open Waitlist' })).toBeInTheDocument();
  });

  it('does not show Add Tee Time Opening button in inactive state', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: null, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.queryByText('Add Tee Time Opening')).not.toBeInTheDocument();
  });

  it('renders open state with short code displayed', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [], openings: [] },
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
      data: { waitlist: closedWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByText('Closed')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Close Waitlist' })).not.toBeInTheDocument();
  });

  it('opens confirmation dialog and calls open mutation when confirmed', async () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: null, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Click the action button to open the confirmation dialog
    fireEvent.click(screen.getByRole('button', { name: 'Open Waitlist' }));

    // Wait for the confirmation dialog to appear
    await waitFor(() => {
      expect(screen.getByText('Open Walk-Up Waitlist')).toBeInTheDocument();
    });

    // Click the AlertDialogAction to confirm
    const confirmButton = document.querySelector('[data-slot="alert-dialog-action"]');
    expect(confirmButton).toBeInTheDocument();
    fireEvent.click(confirmButton!);

    expect(mockOpenMutate).toHaveBeenCalledWith({ courseId: 'course-1' });
  });

  it('shows close confirmation dialog when Close Waitlist button is clicked', async () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [], openings: [] },
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
      data: { waitlist: openWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Open the dialog via the action button
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
      data: { waitlist: openWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(
      screen.getByText('No one is on the walk-up waitlist right now.')
    ).toBeInTheDocument();
  });

  it('renders golfer entries in the queue table', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: mockEntries, openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getAllByText('Alice Smith').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Bob Jones').length).toBeGreaterThan(0);
  });

  it('renders error state when query fails', () => {
    const mockRefetch = vi.fn();
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
      error: new Error('Failed to fetch'),
      refetch: mockRefetch,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByText(/Error loading waitlist: Failed to fetch/)).toBeInTheDocument();

    const retryButton = screen.getByRole('button', { name: 'Retry' });
    expect(retryButton).toBeInTheDocument();

    fireEvent.click(retryButton);
    expect(mockRefetch).toHaveBeenCalled();
  });

  it('shows 409 error message when waitlist is already open', () => {
    const error = Object.assign(new Error('Waitlist is already open for today.'), { status: 409 });

    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: null, entries: [], openings: [] },
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

  it('shows Add Tee Time Opening button when waitlist is open', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.getByRole('button', { name: 'Add Tee Time Opening' })).toBeInTheDocument();
  });

  it('does not show Add Tee Time Opening button when waitlist is closed', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: closedWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.queryByRole('button', { name: 'Add Tee Time Opening' })).not.toBeInTheDocument();
  });

  it('renders Remove buttons with correct aria-labels when waitlist is open', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: mockEntries, openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Both desktop and mobile views render Remove buttons, so we check for at least one of each
    expect(screen.getAllByRole('button', { name: 'Remove Alice Smith from waitlist' })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: 'Remove Bob Jones from waitlist' })).toHaveLength(2);
  });

  it('does not render Remove buttons when waitlist is closed', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: closedWaitlist, entries: mockEntries, openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.queryByRole('button', { name: 'Remove Alice Smith from waitlist' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Remove Bob Jones from waitlist' })).not.toBeInTheDocument();
  });

  it('opens confirmation dialog with golfer name when Remove is clicked', async () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: mockEntries, openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Click the first Remove button (desktop view)
    const removeButtons = screen.getAllByRole('button', { name: 'Remove Alice Smith from waitlist' });
    fireEvent.click(removeButtons[0]!);

    await waitFor(() => {
      expect(screen.getByText('Remove from Waitlist?')).toBeInTheDocument();
    });

    expect(screen.getByText(/Remove Alice Smith from the waitlist/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Keep on Waitlist' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove' })).toBeInTheDocument();
  });

  it('calls remove mutation with correct parameters when confirmed', async () => {
    const mockRemoveMutate = vi.fn();
    mockUseRemoveGolferFromWaitlist.mockReturnValue({
      mutate: mockRemoveMutate,
      isPending: false,
      isSuccess: false,
      isError: false,
      error: null,
      reset: vi.fn(),
    } as unknown as ReturnType<typeof useRemoveGolferFromWaitlist>);

    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: mockEntries, openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    // Click the first Remove button (desktop view)
    const removeButtons = screen.getAllByRole('button', { name: 'Remove Alice Smith from waitlist' });
    fireEvent.click(removeButtons[0]!);

    await waitFor(() => {
      expect(screen.getByText('Remove from Waitlist?')).toBeInTheDocument();
    });

    // Click confirm in the AlertDialog
    const confirmButton = document.querySelector('[data-slot="alert-dialog-action"]');
    expect(confirmButton).toBeInTheDocument();
    fireEvent.click(confirmButton!);

    expect(mockRemoveMutate).toHaveBeenCalledWith(
      { courseId: 'course-1', entryId: 'e-1' },
      expect.objectContaining({
        onSuccess: expect.any(Function),
      })
    );
  });

  it('shows QR code panel when waitlist is open', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: openWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    const qrCanvas = screen.getByTestId('qr-canvas');
    expect(qrCanvas).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /download png/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /print/i })).toBeInTheDocument();
  });

  it('does not show QR code panel when waitlist is closed', () => {
    mockUseWalkUpWaitlistToday.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { waitlist: closedWaitlist, entries: [], openings: [] },
      error: null,
    } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

    render(<WalkUpWaitlist />);

    expect(screen.queryByTestId('qr-canvas')).not.toBeInTheDocument();
  });

  // Note: Tab switching tests are skipped due to complexity with shadcn Tabs component in test environment
  // The Cancel button functionality is verified through the CancelOpeningDialog component tests
});
