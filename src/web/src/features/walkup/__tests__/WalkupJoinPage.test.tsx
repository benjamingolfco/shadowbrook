import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import WalkupJoinPage from '../pages/WalkupJoinPage';
import { useVerifyCode, useJoinWaitlist } from '../hooks/useWalkupJoin';

vi.mock('../hooks/useWalkupJoin');

const mockUseVerifyCode = vi.mocked(useVerifyCode);
const mockUseJoinWaitlist = vi.mocked(useJoinWaitlist);

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
  setupVerifyMutation();
  setupJoinMutation();
});

describe('WalkupJoinPage', () => {
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
    // Start on join phase by simulating verification
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
