import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import WalkupJoinPage from '../pages/WalkupJoinPage';
import { useVerifyCode, useJoinWaitlist } from '../hooks/useWalkupJoin';
import { useWalkUpStatus } from '@/hooks/useWalkUpStatus';

vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useParams: () => ({ shortCode: 'abc123' }),
  };
});

vi.mock('../hooks/useWalkupJoin');
vi.mock('@/hooks/useWalkUpStatus');

const mockUseVerifyCode = vi.mocked(useVerifyCode);
const mockUseJoinWaitlist = vi.mocked(useJoinWaitlist);
const mockUseWalkUpStatus = vi.mocked(useWalkUpStatus);

const verifyData = {
  courseWaitlistId: 'wl-1',
  courseName: 'Pine Valley Golf Club',
  shortCode: '4827',
};

const joinResult = {
  entryId: 'e-1',
  golferName: 'John Smith',
  position: 1,
  courseName: 'Pine Valley Golf Club',
};

function setupStatusHook(overrides = {}) {
  mockUseWalkUpStatus.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...overrides,
  } as unknown as ReturnType<typeof useWalkUpStatus>);
}

function setupVerifyMutation(overrides = {}) {
  mockUseVerifyCode.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useVerifyCode>);
}

function setupJoinMutation(overrides = {}) {
  mockUseJoinWaitlist.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useJoinWaitlist>);
}

beforeEach(() => {
  vi.clearAllMocks();
  setupStatusHook();
  setupVerifyMutation();
  setupJoinMutation();
});

describe('WalkupJoinPage', () => {
  describe('form flow (open status)', () => {
    it('renders code entry phase initially', () => {
      render(<WalkupJoinPage />);

      expect(screen.getByText('Enter the 4-digit code posted at the course')).toBeInTheDocument();
      expect(screen.queryByText('Join Waitlist')).not.toBeInTheDocument();
    });

    it('shows Shadowbrook wordmark', () => {
      render(<WalkupJoinPage />);

      expect(screen.getByRole('heading', { name: 'Shadowbrook', level: 1 })).toBeInTheDocument();
    });

    it('transitions to join form after successful code verification', async () => {
      mockUseVerifyCode.mockReturnValue({
        mutate: vi.fn((_, options) => options?.onSuccess?.(verifyData)),
        isPending: false,
        isError: false,
        isSuccess: false,
        error: null,
      } as unknown as ReturnType<typeof useVerifyCode>);

      render(<WalkupJoinPage />);

      const input = screen.getByRole('textbox');
      fireEvent.change(input, { target: { value: '4827' } });

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'Pine Valley Golf Club' })).toBeInTheDocument();
      });

      expect(screen.getByRole('button', { name: 'Join Waitlist' })).toBeInTheDocument();
    });

    it('transitions to confirmation after successful join', async () => {
      mockUseVerifyCode.mockReturnValue({
        mutate: vi.fn((_, options) => options?.onSuccess?.(verifyData)),
        isPending: false,
        isError: false,
        isSuccess: false,
        error: null,
      } as unknown as ReturnType<typeof useVerifyCode>);

      mockUseJoinWaitlist.mockReturnValue({
        mutate: vi.fn((_, options) => options?.onSuccess?.(joinResult)),
        isPending: false,
        isError: false,
        isSuccess: false,
        error: null,
      } as unknown as ReturnType<typeof useJoinWaitlist>);

      render(<WalkupJoinPage />);

      // Move to join phase
      fireEvent.change(screen.getByRole('textbox'), { target: { value: '4827' } });

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Join Waitlist' })).toBeInTheDocument();
      });

      // Fill out and submit join form
      fireEvent.change(screen.getByLabelText('First Name'), { target: { value: 'John' } });
      fireEvent.change(screen.getByLabelText('Last Name'), { target: { value: 'Smith' } });
      fireEvent.change(screen.getByLabelText('Phone Number'), { target: { value: '5551234567' } });
      fireEvent.click(screen.getByRole('button', { name: 'Join Waitlist' }));

      await waitFor(() => {
        expect(screen.getByText("You're on the list, John!")).toBeInTheDocument();
      });
    });
  });

  describe('status check states', () => {
    it('renders loading skeleton while status is fetching', () => {
      setupStatusHook({ isLoading: true });

      render(<WalkupJoinPage />);

      expect(document.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThan(0);
      expect(screen.getByText('Loading waitlist status')).toBeInTheDocument();
    });

    it('renders Invalid Code for 404 error', () => {
      const error = Object.assign(new Error('Not found'), { status: 404 });
      setupStatusHook({ isError: true, error });

      render(<WalkupJoinPage />);

      expect(screen.getByText('Invalid Code')).toBeInTheDocument();
      expect(screen.getByText('This QR code is not valid.')).toBeInTheDocument();
      expect(
        screen.getByText('Please ask for the current walk-up waitlist code at the pro shop.')
      ).toBeInTheDocument();
    });

    it('renders network error with retry button', () => {
      const mockRefetch = vi.fn();
      const error = new Error('Network error');
      setupStatusHook({ isError: true, error, refetch: mockRefetch });

      render(<WalkupJoinPage />);

      expect(screen.getByText('Something went wrong')).toBeInTheDocument();
      expect(screen.getByText('Unable to load waitlist status')).toBeInTheDocument();

      const retryButton = screen.getByRole('button', { name: 'Try Again' });
      fireEvent.click(retryButton);
      expect(mockRefetch).toHaveBeenCalledOnce();
    });

    it('renders Waitlist Closed state', () => {
      setupStatusHook({
        data: { status: 'closed', courseName: 'Pine Valley Golf Club', date: '2026-03-29' },
      });

      render(<WalkupJoinPage />);

      expect(screen.getByText('Waitlist Closed')).toBeInTheDocument();
      expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
      expect(
        screen.getByText('The walk-up waitlist is closed for today. No new entries are being accepted.')
      ).toBeInTheDocument();
    });

    it('renders Code Expired state with formatted date', () => {
      setupStatusHook({
        data: { status: 'expired', courseName: 'Pine Valley Golf Club', date: '2026-03-22' },
      });

      render(<WalkupJoinPage />);

      expect(screen.getByText('Code Expired')).toBeInTheDocument();
      expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
      expect(screen.getByText(/It was valid on 3\/22\/2026/)).toBeInTheDocument();
      expect(screen.getByText(/Please ask for today's code at the pro shop/)).toBeInTheDocument();
    });

    it('renders form when status is open', () => {
      setupStatusHook({
        data: { status: 'open', courseName: 'Pine Valley Golf Club', date: '2026-03-29' },
      });

      render(<WalkupJoinPage />);

      expect(screen.getByText('Enter the 4-digit code posted at the course')).toBeInTheDocument();
    });
  });
});
