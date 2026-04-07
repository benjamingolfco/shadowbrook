import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';
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
// Pin the viewport to "wide" so the queue renders inline via the right rail.
// Narrow-viewport sheet behavior is exercised by the header's onOpenQueue test.
vi.mock('@/hooks/use-media-query', () => ({
  useMediaQuery: () => true,
}));
vi.mock('@/components/layout/PageRightRail', () => ({
  PageRightRail: ({ children }: { children: ReactNode }) => (
    <div data-testid="page-right-rail">{children}</div>
  ),
}));
vi.mock('../components/PostTeeTimeForm', () => ({
  PostTeeTimeForm: ({ courseId }: { courseId: string }) => (
    <div data-testid="post-tee-time-form" data-course-id={courseId}>
      <button type="submit">Post Tee Time</button>
    </div>
  ),
}));
vi.mock('../components/OpeningsGrid', () => ({
  OpeningsGrid: ({
    openings,
    headerAction,
  }: {
    openings: unknown[];
    headerAction?: ReactNode;
  }) => (
    <div data-testid="openings-list" data-count={openings.length}>
      {headerAction}
    </div>
  ),
}));
vi.mock('../components/QueuePanel', () => ({
  QueuePanel: ({
    entries,
    isWaitlistOpen,
    onAddGolfer,
  }: {
    entries: { id: string; golferName: string }[];
    isWaitlistOpen: boolean;
    onAddGolfer: () => void;
  }) => (
    <div data-testid="queue-panel" data-count={entries.length}>
      {isWaitlistOpen && (
        <button type="button" onClick={onAddGolfer}>Add golfer</button>
      )}
      <ul>
        {entries.map((entry) => (
          <li key={entry.id}>{entry.golferName}</li>
        ))}
      </ul>
    </div>
  ),
}));
vi.mock('../components/WalkUpWaitlistPageHeader', () => ({
  WalkUpWaitlistPageHeader: ({
    status,
    shortCode,
    queueCount,
    hideQueueCount,
    onPrintSign,
    onClose,
    onReopen,
    onOpenQueue,
  }: {
    status: 'Open' | 'Closed';
    shortCode: string;
    queueCount: number;
    hideQueueCount?: boolean;
    onPrintSign: () => void;
    onClose: () => void;
    onReopen: () => void;
    onOpenQueue: () => void;
  }) => (
    <div data-testid="walkup-waitlist-page-header">
      <span>{status}</span>
      <span data-testid="short-code">{shortCode.split('').join(' ')}</span>
      {!hideQueueCount && (
        <button type="button" onClick={onOpenQueue}>
          {queueCount} waiting
        </button>
      )}
      {status === 'Open' && (
        <>
          <button type="button" onClick={onPrintSign}>Print sign</button>
          <button type="button" onClick={onClose}>Close waitlist</button>
        </>
      )}
      {status === 'Closed' && (
        <button type="button" onClick={onReopen}>Reopen</button>
      )}
    </div>
  ),
}));
vi.mock('../components/CloseWaitlistDialog', () => ({
  CloseWaitlistDialog: ({
    open,
    onConfirm,
  }: {
    open: boolean;
    onOpenChange: (v: boolean) => void;
    onConfirm: () => void;
  }) =>
    open ? (
      <div role="dialog" aria-label="Close Waitlist">
        <p>Close Walk-Up Waitlist?</p>
        <button type="button" onClick={onConfirm}>
          Confirm Close
        </button>
      </div>
    ) : null,
}));
vi.mock('../components/ReopenWaitlistDialog', () => ({
  ReopenWaitlistDialog: ({
    open,
    onConfirm,
  }: {
    open: boolean;
    onOpenChange: (v: boolean) => void;
    onConfirm: () => void;
  }) =>
    open ? (
      <div role="dialog" aria-label="Reopen Waitlist">
        <button type="button" onClick={onConfirm}>
          Confirm Reopen
        </button>
      </div>
    ) : null,
}));
vi.mock('../components/AddGolferDialog', () => ({
  AddGolferDialog: ({ open }: { open: boolean; onOpenChange: (v: boolean) => void; courseId: string }) =>
    open ? <div role="dialog" aria-label="Add Golfer" /> : null,
}));
vi.mock('../components/RemoveGolferDialog', () => ({
  RemoveGolferDialog: ({
    open,
    onConfirm,
    golferName,
  }: {
    open: boolean;
    onOpenChange: (v: boolean) => void;
    onConfirm: () => void;
    golferName: string;
    isPending: boolean;
  }) =>
    open ? (
      <div role="dialog" aria-label="Remove Golfer">
        <p>Remove {golferName} from the waitlist</p>
        <button type="button" onClick={onConfirm}>
          Remove
        </button>
      </div>
    ) : null,
}));
vi.mock('../components/CancelOpeningDialog', () => ({
  CancelOpeningDialog: ({
    open,
    onConfirm,
  }: {
    open: boolean;
    onOpenChange: (v: boolean) => void;
    onConfirm: () => void;
    isPending: boolean;
  }) =>
    open ? (
      <div role="dialog" aria-label="Cancel Opening">
        <button type="button" onClick={onConfirm}>
          Confirm Cancel
        </button>
      </div>
    ) : null,
}));
vi.mock('../components/QrCodePanel', () => ({
  QrCodePanel: ({ shortCode }: { shortCode: string }) => (
    <div data-testid="qr-code-panel" data-short-code={shortCode} />
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
  describe('Loading state', () => {
    it('renders skeleton while loading', () => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: true,
        isError: false,
        data: undefined,
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

      render(<WalkUpWaitlist />);

      expect(document.querySelector('[data-slot="skeleton"]')).toBeInTheDocument();
    });
  });

  describe('Error state', () => {
    it('renders error message and Retry button when query fails', () => {
      const mockRefetch = vi.fn();
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: true,
        data: undefined,
        error: new Error('Network error'),
        refetch: mockRefetch,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

      render(<WalkUpWaitlist />);

      expect(screen.getByText("Couldn't load waitlist")).toBeInTheDocument();
      const retryButton = screen.getByRole('button', { name: 'Retry' });
      expect(retryButton).toBeInTheDocument();
    });

    it('calls refetch when Retry is clicked', () => {
      const mockRefetch = vi.fn();
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: true,
        data: undefined,
        error: new Error('Network error'),
        refetch: mockRefetch,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
      expect(mockRefetch).toHaveBeenCalled();
    });
  });

  describe('Inactive state (no waitlist today)', () => {
    beforeEach(() => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: false,
        data: { waitlist: null, entries: [], openings: [] },
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);
    });

    it('renders "Open Waitlist for Today" button', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByRole('button', { name: 'Open Waitlist for Today' })).toBeInTheDocument();
    });

    it('fires open mutation directly when button is clicked (no dialog)', () => {
      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByRole('button', { name: 'Open Waitlist for Today' }));

      expect(mockOpenMutate).toHaveBeenCalledWith({ courseId: 'course-1' });
    });

    it('does not open a confirmation dialog when button is clicked', () => {
      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByRole('button', { name: 'Open Waitlist for Today' }));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('does not render PostTeeTimeForm in inactive state', () => {
      render(<WalkUpWaitlist />);
      expect(screen.queryByTestId('post-tee-time-form')).not.toBeInTheDocument();
    });

    it('shows 409 error inline when waitlist is already open', () => {
      const error = Object.assign(new Error('Waitlist is already open for today.'), { status: 409 });
      mockUseOpenWalkUpWaitlist.mockReturnValue({
        mutate: mockOpenMutate,
        isPending: false,
        isError: true,
        isSuccess: false,
        error,
      } as unknown as ReturnType<typeof useOpenWalkUpWaitlist>);

      render(<WalkUpWaitlist />);

      expect(screen.getByText('Waitlist is already open — try refreshing the page.')).toBeInTheDocument();
    });
  });

  describe('Active state (Open)', () => {
    beforeEach(() => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: false,
        data: { waitlist: openWaitlist, entries: [], openings: [] },
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);
    });

    it('displays short code with spaces between digits', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByText('4 8 2 7')).toBeInTheDocument();
    });

    it('shows Open badge', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByText('Open')).toBeInTheDocument();
    });

    it('renders inline PostTeeTimeForm', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByTestId('post-tee-time-form')).toBeInTheDocument();
    });

    it('renders queue panel with entry count in the right rail', () => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: false,
        data: { waitlist: openWaitlist, entries: mockEntries, openings: [] },
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

      render(<WalkUpWaitlist />);

      const rail = screen.getByTestId('page-right-rail');
      const panel = screen.getByTestId('queue-panel');
      expect(rail).toContainElement(panel);
      expect(panel).toHaveAttribute('data-count', '2');
    });

    it('renders golfer names in the queue panel at wide widths', () => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: false,
        data: { waitlist: openWaitlist, entries: mockEntries, openings: [] },
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);

      render(<WalkUpWaitlist />);

      expect(screen.getByText('Alice Smith')).toBeInTheDocument();
      expect(screen.getByText('Bob Jones')).toBeInTheDocument();
    });

    it('does not render the waiting count button in the page header at wide widths', () => {
      render(<WalkUpWaitlist />);
      expect(screen.queryByRole('button', { name: /waiting/ })).not.toBeInTheDocument();
    });

    it('renders openings list', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByTestId('openings-list')).toBeInTheDocument();
    });

    it('renders "Add golfer" link', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByText('Add golfer')).toBeInTheDocument();
    });

    it('renders "Close waitlist" text link', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByText('Close waitlist')).toBeInTheDocument();
    });

    it('does NOT show a "Print sign" button area as a standalone QR code', () => {
      render(<WalkUpWaitlist />);
      // QR code is behind dialog trigger, not directly visible
      expect(screen.queryByTestId('qr-code-panel')).not.toBeInTheDocument();
    });

    it('"Print sign" trigger is present and opens QR code dialog when clicked', async () => {
      render(<WalkUpWaitlist />);

      const printTrigger = screen.getByText('Print sign');
      expect(printTrigger).toBeInTheDocument();

      fireEvent.click(printTrigger);

      await waitFor(() => {
        expect(screen.getByTestId('qr-code-panel')).toBeInTheDocument();
      });
    });
  });

  describe('Close waitlist', () => {
    beforeEach(() => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: false,
        data: { waitlist: openWaitlist, entries: [], openings: [] },
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);
    });

    it('opens CloseWaitlistDialog when "Close waitlist" is clicked', async () => {
      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByText('Close waitlist'));

      await waitFor(() => {
        expect(screen.getByText('Close Walk-Up Waitlist?')).toBeInTheDocument();
      });
    });

    it('calls close mutation when dialog is confirmed', async () => {
      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByText('Close waitlist'));

      await waitFor(() => {
        expect(screen.getByText('Close Walk-Up Waitlist?')).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Confirm Close' }));

      expect(mockCloseMutate).toHaveBeenCalledWith({ courseId: 'course-1' });
    });
  });

  describe('Closed state', () => {
    beforeEach(() => {
      mockUseWalkUpWaitlistToday.mockReturnValue({
        isLoading: false,
        isError: false,
        data: { waitlist: closedWaitlist, entries: [], openings: [] },
        error: null,
      } as unknown as ReturnType<typeof useWalkUpWaitlistToday>);
    });

    it('renders Closed badge', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByText('Closed')).toBeInTheDocument();
    });

    it('renders Reopen button', () => {
      render(<WalkUpWaitlist />);
      expect(screen.getByRole('button', { name: 'Reopen' })).toBeInTheDocument();
    });

    it('does not render PostTeeTimeForm', () => {
      render(<WalkUpWaitlist />);
      expect(screen.queryByTestId('post-tee-time-form')).not.toBeInTheDocument();
    });

    it('does not render "Close waitlist" link', () => {
      render(<WalkUpWaitlist />);
      expect(screen.queryByText('Close waitlist')).not.toBeInTheDocument();
    });

    it('opens ReopenWaitlistDialog when Reopen is clicked', async () => {
      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByRole('button', { name: 'Reopen' }));

      await waitFor(() => {
        expect(screen.getByRole('dialog', { name: 'Reopen Waitlist' })).toBeInTheDocument();
      });
    });

    it('calls reopen mutation when dialog is confirmed', async () => {
      render(<WalkUpWaitlist />);

      fireEvent.click(screen.getByRole('button', { name: 'Reopen' }));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Confirm Reopen' })).toBeInTheDocument();
      });

      fireEvent.click(screen.getByRole('button', { name: 'Confirm Reopen' }));

      expect(mockReopenMutate).toHaveBeenCalledWith({ courseId: 'course-1' });
    });
  });
});
