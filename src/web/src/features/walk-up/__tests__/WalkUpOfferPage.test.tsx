import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@/test/test-utils';
import WalkUpOfferPage from '../pages/WalkUpOfferPage';
import type { WaitlistOfferResponse } from '@/types/waitlist';

const pendingOffer: WaitlistOfferResponse = {
  token: 'abc-123',
  courseName: 'Pine Valley Golf Club',
  teeTime: '2026-04-15T09:30:00',
  slotsAvailable: 2,
  golferName: 'John Smith',
  status: 'Pending',
};

const acceptedOffer: WaitlistOfferResponse = {
  ...pendingOffer,
  status: 'Accepted',
};

const rejectedOffer: WaitlistOfferResponse = {
  ...pendingOffer,
  status: 'Rejected',
};

vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useParams: () => ({ token: 'abc-123' }),
  };
});

vi.mock('../hooks/useWalkUpOffer', () => ({
  useWalkUpOffer: vi.fn(),
  useAcceptOffer: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
}));

import { useWalkUpOffer } from '../hooks/useWalkUpOffer';

function mockOffer(offer: WaitlistOfferResponse | null, overrides?: { isLoading?: boolean; error?: Error }) {
  vi.mocked(useWalkUpOffer).mockReturnValue({
    data: offer ?? undefined,
    isLoading: overrides?.isLoading ?? false,
    error: overrides?.error ?? null,
    refetch: vi.fn(),
  } as unknown as ReturnType<typeof useWalkUpOffer>);
}

describe('WalkUpOfferPage', () => {
  it('shows claim button for pending offer', () => {
    mockOffer(pendingOffer);
    render(<WalkUpOfferPage />);

    expect(screen.getByText('Claim This Tee Time')).toBeInTheDocument();
  });

  it('shows confirmation for accepted offer', () => {
    mockOffer(acceptedOffer);
    render(<WalkUpOfferPage />);

    expect(screen.getByText('Tee Time Claimed')).toBeInTheDocument();
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.queryByText('Claim This Tee Time')).not.toBeInTheDocument();
  });

  it('shows unavailable message for rejected offer', () => {
    mockOffer(rejectedOffer);
    render(<WalkUpOfferPage />);

    expect(screen.getByText('Offer No Longer Available')).toBeInTheDocument();
    expect(screen.queryByText('Claim This Tee Time')).not.toBeInTheDocument();
  });

  it('does not show claim button or status messages while loading', () => {
    mockOffer(null, { isLoading: true });
    render(<WalkUpOfferPage />);

    expect(screen.queryByText('Claim This Tee Time')).not.toBeInTheDocument();
    expect(screen.queryByText('Tee Time Claimed')).not.toBeInTheDocument();
    expect(screen.queryByText('Offer No Longer Available')).not.toBeInTheDocument();
  });
});
