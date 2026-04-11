import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import AcceptConfirmation from '../components/AcceptConfirmation';
import type { WaitlistOfferAcceptResponse, WaitlistOfferResponse } from '@/types/waitlist';

const offer: WaitlistOfferResponse = {
  token: 'abc-123',
  courseName: 'Pine Valley Golf Club',
  teeTime: '2026-04-15T09:30:00',
  slotsAvailable: 2,
  golferName: 'John Smith',
  status: 'Accepted',
};

const response: WaitlistOfferAcceptResponse = {
  status: 'Accepted',
  message: 'Your tee time has been confirmed!',
  golferId: 'g-1',
};

describe('AcceptConfirmation', () => {
  it('shows "Tee Time Claimed" heading', () => {
    render(<AcceptConfirmation offer={offer} response={response} />);

    expect(screen.getByText('Tee Time Claimed')).toBeInTheDocument();
  });

  it('shows the API response message when provided', () => {
    render(<AcceptConfirmation offer={offer} response={response} />);

    expect(screen.getByText('Your tee time has been confirmed!')).toBeInTheDocument();
  });

  it('shows course name', () => {
    render(<AcceptConfirmation offer={offer} response={response} />);

    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
  });

  it('shows formatted date and time', () => {
    render(<AcceptConfirmation offer={offer} response={response} />);

    expect(screen.getByText(/Wednesday, April 15, 2026/)).toBeInTheDocument();
    expect(screen.getByText(/9:30 AM/)).toBeInTheDocument();
  });

  it('renders without response prop (page reload case)', () => {
    render(<AcceptConfirmation offer={offer} />);

    expect(screen.getByText('Tee Time Claimed')).toBeInTheDocument();
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.queryByText('Your tee time has been confirmed!')).not.toBeInTheDocument();
  });

  it('renders a green checkmark icon', () => {
    const { container } = render(<AcceptConfirmation offer={offer} />);

    const iconWrapper = container.querySelector('.bg-green-light');
    expect(iconWrapper).toBeInTheDocument();

    const svg = iconWrapper?.querySelector('svg.text-green');
    expect(svg).toBeInTheDocument();
  });

  it('shows dev SMS link in dev mode', () => {
    render(<AcceptConfirmation offer={offer} response={response} />);

    const link = screen.getByText('View SMS messages');
    expect(link).toBeInTheDocument();
    expect(link.closest('a')).toHaveAttribute('href', '/dev/sms/golfer/g-1');
  });
});
