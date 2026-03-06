import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import Confirmation from '../components/Confirmation';
import type { JoinWaitlistResponse } from '@/types/waitlist';

const result: JoinWaitlistResponse = {
  entryId: 'e-1',
  golferName: 'John Smith',
  position: 3,
  courseName: 'Pine Valley Golf Club',
};

describe('Confirmation', () => {
  it('shows golfer first name in heading', () => {
    render(<Confirmation result={result} />);

    expect(screen.getByText("You're on the list, John!")).toBeInTheDocument();
  });

  it('shows position number', () => {
    render(<Confirmation result={result} />);

    expect(screen.getByText(/#3 in line at/)).toBeInTheDocument();
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
});
