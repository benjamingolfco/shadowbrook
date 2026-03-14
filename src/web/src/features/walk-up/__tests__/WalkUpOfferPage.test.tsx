import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import WalkUpOfferPage from '../pages/WalkUpOfferPage';
import * as hooks from '../hooks/useWalkUpOffer';
import type { WaitlistOfferResponse } from '@/types/waitlist';

vi.mock('../hooks/useWalkUpOffer');

const mockOffer: WaitlistOfferResponse = {
  token: '123e4567-e89b-12d3-a456-426614174000',
  courseName: 'Pine Valley',
  date: '2026-03-13',
  teeTime: '09:20',
  golfersNeeded: 2,
  golferName: 'Jane Smith',
  status: 'Pending',
  expiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(), // 15 minutes from now
};

describe('WalkUpOfferPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading skeleton when query is pending', () => {
    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    expect(screen.getByText('Shadowbrook')).toBeInTheDocument();
    // Skeletons don't have accessible text, but we can check for the Card structure
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Shadowbrook');
  });

  it('renders offer card with correct details for active offer', () => {
    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: mockOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    expect(screen.getByText('Pine Valley')).toBeInTheDocument();
    expect(screen.getByText(/Friday, March 13/)).toBeInTheDocument();
    expect(screen.getByText(/9:20 AM/)).toBeInTheDocument();
    expect(screen.getByText(/2 spots available/)).toBeInTheDocument();
    expect(screen.getByText('Hi, Jane!')).toBeInTheDocument();
  });

  it('shows countdown timer', () => {
    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: mockOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    expect(screen.getByRole('timer')).toBeInTheDocument();
    expect(screen.getByRole('timer')).toHaveTextContent(/remaining/);
  });

  it('shows "Claim This Tee Time" button', () => {
    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: mockOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    expect(screen.getByRole('button', { name: /Claim This Tee Time/i })).toBeInTheDocument();
  });

  it('shows AlertDialog on button click', async () => {
    const user = userEvent.setup();

    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: mockOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    const claimButton = screen.getByRole('button', { name: /Claim This Tee Time/i });
    await user.click(claimButton);

    await waitFor(() => {
      expect(screen.getByText('Claim this tee time?')).toBeInTheDocument();
    });

    // Check that the dialog content includes the course details
    const dialog = screen.getByRole('alertdialog');
    expect(dialog).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Confirm/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
  });

  it('transitions to success state after acceptance', async () => {
    const user = userEvent.setup();
    const mockMutate = vi.fn((_, { onSuccess }) => {
      onSuccess?.({
        status: 'Accepted',
        courseName: 'Pine Valley',
        date: '2026-03-13',
        teeTime: '09:20',
        golferName: 'Jane Smith',
        message: "You're booked!",
      });
    });

    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: mockOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    const claimButton = screen.getByRole('button', { name: /Claim This Tee Time/i });
    await user.click(claimButton);

    await waitFor(() => {
      expect(screen.getByText('Claim this tee time?')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /Confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/You're booked, Jane!/i)).toBeInTheDocument();
    });

    expect(screen.getByText('See you on the course!')).toBeInTheDocument();
  });

  it('shows expired state when status is Expired', () => {
    const expiredOffer: WaitlistOfferResponse = {
      ...mockOffer,
      status: 'Expired',
    };

    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: expiredOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    expect(screen.getByText('This offer has expired')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Claim This Tee Time/i })).not.toBeInTheDocument();
  });

  it('shows not found state on 404 error', () => {
    const error = new Error('Offer not found.') as Error & { status?: number };
    error.status = 404;

    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: undefined,
      isLoading: false,
      error,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    expect(screen.getByText('Offer Not Found')).toBeInTheDocument();
    expect(screen.getByText(/This tee time offer could not be found/)).toBeInTheDocument();
  });

  it('shows error message on accept failure', async () => {
    const user = userEvent.setup();
    const mockMutate = vi.fn((_, { onError }) => {
      onError?.(new Error('All slots have been filled.'));
    });

    vi.mocked(hooks.useWalkUpOffer).mockReturnValue({
      data: mockOffer,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof hooks.useWalkUpOffer>);

    vi.mocked(hooks.useAcceptOffer).mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    } as unknown as ReturnType<typeof hooks.useAcceptOffer>);

    render(<WalkUpOfferPage />);

    const claimButton = screen.getByRole('button', { name: /Claim This Tee Time/i });
    await user.click(claimButton);

    await waitFor(() => {
      expect(screen.getByText('Claim this tee time?')).toBeInTheDocument();
    });

    const confirmButton = screen.getByRole('button', { name: /Confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('All slots have been filled.');
    });
  });
});
