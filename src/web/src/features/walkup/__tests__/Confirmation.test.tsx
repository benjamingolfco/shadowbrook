import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import Confirmation from '../components/Confirmation';
import type { JoinWaitlistResponse } from '@/types/waitlist';

const result: JoinWaitlistResponse = {
  entryId: 'e-1',
  golferId: 'g-1',
  golferName: 'John Smith',
  position: 3,
  courseName: 'Pine Valley Golf Club',
};

describe('Confirmation', () => {
  it('shows golfer first name in heading', () => {
    render(<Confirmation result={result} />);

    expect(screen.getByText("You're on the list, John!")).toBeInTheDocument();
  });

  it('shows position number when position > 0', () => {
    render(<Confirmation result={result} />);

    expect(screen.getByText(/#3 in line at/)).toBeInTheDocument();
  });

  it('shows only course name when position is 0', () => {
    render(<Confirmation result={{ ...result, position: 0 }} />);

    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.queryByText(/#\d+ in line/)).not.toBeInTheDocument();
  });

  it('shows course name', () => {
    render(<Confirmation result={result} />);

    expect(screen.getByText(/Pine Valley Golf Club/)).toBeInTheDocument();
  });

  it('shows keep your phone handy text', () => {
    render(<Confirmation result={result} />);

    expect(
      screen.getByText(/Keep your phone handy/),
    ).toBeInTheDocument();
  });

  it('extracts first name from full name with multiple parts', () => {
    render(
      <Confirmation
        result={{ ...result, golferName: 'Mary Jane Watson' }}
      />,
    );

    expect(screen.getByText("You're on the list, Mary!")).toBeInTheDocument();
  });

  it('shows dev SMS link in dev mode', () => {
    render(<Confirmation result={result} />);

    const link = screen.getByText('View SMS messages');
    expect(link).toBeInTheDocument();
    expect(link.closest('a')).toHaveAttribute('href', '/dev/sms/golfer/g-1');
  });
});
