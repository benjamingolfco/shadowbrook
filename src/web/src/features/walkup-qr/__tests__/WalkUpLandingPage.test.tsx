import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import WalkUpLandingPage from '../pages/WalkUpLandingPage';
import { useWalkUpStatus } from '../hooks/useWalkUpStatus';

vi.mock('../hooks/useWalkUpStatus');

const mockUseWalkUpStatus = vi.mocked(useWalkUpStatus);

const mockOpenStatus = {
  status: 'open' as const,
  courseName: 'Pine Valley Golf Club',
  date: '2026-03-23',
};

const mockClosedStatus = {
  status: 'closed' as const,
  courseName: 'Pine Valley Golf Club',
  date: '2026-03-23',
};

const mockExpiredStatus = {
  status: 'expired' as const,
  courseName: 'Pine Valley Golf Club',
  date: '2026-03-22',
};

function setupHook(overrides = {}) {
  mockUseWalkUpStatus.mockReturnValue({
    data: undefined,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
    ...overrides,
  } as unknown as ReturnType<typeof useWalkUpStatus>);
}

beforeEach(() => {
  vi.clearAllMocks();
  setupHook();
});

describe('WalkUpLandingPage', () => {
  it('renders loading state with skeleton', () => {
    setupHook({ isLoading: true });

    render(<WalkUpLandingPage />);

    expect(document.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThan(0);
    expect(screen.getByText('Loading waitlist status')).toBeInTheDocument();
  });

  it('renders open state with course name and join link', () => {
    setupHook({ data: mockOpenStatus });

    render(<WalkUpLandingPage />);

    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.getByText('Walk-Up Waitlist')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Join Waitlist' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Join Waitlist' })).toHaveAttribute('href', '/join/undefined');
  });

  it('renders closed state with clock icon and message', () => {
    setupHook({ data: mockClosedStatus });

    render(<WalkUpLandingPage />);

    expect(screen.getByText('Waitlist Closed')).toBeInTheDocument();
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(
      screen.getByText('The walk-up waitlist is closed for today. No new entries are being accepted.')
    ).toBeInTheDocument();
  });

  it('renders expired state with calendar icon and message', () => {
    setupHook({ data: mockExpiredStatus });

    render(<WalkUpLandingPage />);

    expect(screen.getByText('Code Expired')).toBeInTheDocument();
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.getByText(/This QR code is no longer valid/)).toBeInTheDocument();
    expect(screen.getByText(/Please ask for today's code at the pro shop/)).toBeInTheDocument();
  });

  it('renders 404 error state as invalid code', () => {
    const error = Object.assign(new Error('Not found'), { status: 404 });
    setupHook({ isError: true, error });

    render(<WalkUpLandingPage />);

    expect(screen.getByText('Invalid Code')).toBeInTheDocument();
    expect(screen.getByText('This QR code is not valid.')).toBeInTheDocument();
    expect(
      screen.getByText('Please ask for the current walk-up waitlist code at the pro shop.')
    ).toBeInTheDocument();
  });

  it('renders network error state with retry button', () => {
    const mockRefetch = vi.fn();
    const error = new Error('Network error');
    setupHook({ isError: true, error, refetch: mockRefetch });

    render(<WalkUpLandingPage />);

    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByText('Unable to load waitlist status')).toBeInTheDocument();

    const retryButton = screen.getByRole('button', { name: 'Try Again' });
    expect(retryButton).toBeInTheDocument();

    fireEvent.click(retryButton);
    expect(mockRefetch).toHaveBeenCalledOnce();
  });

  it('updates document title when data loads', () => {
    setupHook({ data: mockOpenStatus });

    render(<WalkUpLandingPage />);

    expect(document.title).toBe('Walk-Up Waitlist - Pine Valley Golf Club');
  });

  it('sets default document title when loading', () => {
    setupHook({ isLoading: true });

    render(<WalkUpLandingPage />);

    expect(document.title).toBe('Walk-Up Waitlist');
  });

  it('renders date correctly without UTC timezone shift (open state)', () => {
    const testStatus = {
      status: 'open' as const,
      courseName: 'Test Course',
      date: '2026-03-26',
    };
    setupHook({ data: testStatus });

    render(<WalkUpLandingPage />);

    expect(screen.getByText(/The walk-up waitlist is open for 3\/26\/2026/)).toBeInTheDocument();
  });

  it('renders date correctly without UTC timezone shift (expired state)', () => {
    const testStatus = {
      status: 'expired' as const,
      courseName: 'Test Course',
      date: '2026-03-26',
    };
    setupHook({ data: testStatus });

    render(<WalkUpLandingPage />);

    expect(screen.getByText(/It was valid on 3\/26\/2026/)).toBeInTheDocument();
  });
});
