import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@/test/test-utils';
import { OpeningsList } from '../components/OpeningsList';
import type { WaitlistOpeningEntry } from '@/types/waitlist';

const openOpening: WaitlistOpeningEntry = {
  id: 'o-1',
  teeTime: '2026-06-01T10:40:00',
  slotsAvailable: 4,
  slotsRemaining: 2,
  status: 'Open',
  filledGolfers: [
    { golferId: 'g-1', golferName: 'Alice Smith', groupSize: 1 },
    { golferId: 'g-2', golferName: 'Bob Jones', groupSize: 1 },
  ],
};

const filledOpening: WaitlistOpeningEntry = {
  id: 'o-2',
  teeTime: '2026-06-01T08:00:00',
  slotsAvailable: 4,
  slotsRemaining: 0,
  status: 'Filled',
  filledGolfers: [
    { golferId: 'g-1', golferName: 'Alice Smith', groupSize: 2 },
    { golferId: 'g-3', golferName: 'Charlie Brown', groupSize: 2 },
  ],
};

const cancelledOpening: WaitlistOpeningEntry = {
  id: 'o-3',
  teeTime: '2026-06-01T14:00:00',
  slotsAvailable: 4,
  slotsRemaining: 4,
  status: 'Cancelled',
  filledGolfers: [],
};

describe('OpeningsList', () => {
  it('renders empty state when no openings', () => {
    render(<OpeningsList openings={[]} onCancel={vi.fn()} cancellingId={null} />);
    expect(screen.getByText('No openings posted yet.')).toBeInTheDocument();
  });

  it('renders opening times in sorted order', () => {
    render(
      <OpeningsList
        openings={[openOpening, filledOpening, cancelledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    const text = document.body.textContent ?? '';
    const idx8 = text.indexOf('8:00 AM');
    const idx10 = text.indexOf('10:40 AM');
    const idx14 = text.indexOf('2:00 PM');
    expect(idx8).toBeLessThan(idx10);
    expect(idx10).toBeLessThan(idx14);
  });

  it('shows status badges for each opening', () => {
    render(
      <OpeningsList
        openings={[openOpening, filledOpening, cancelledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    // Each status appears in both the mobile and desktop layout
    expect(screen.getAllByText('Open').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Filled').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('Cancelled').length).toBeGreaterThanOrEqual(1);
  });

  it('shows fill count text for open openings', () => {
    render(
      <OpeningsList openings={[openOpening]} onCancel={vi.fn()} cancellingId={null} />,
    );

    expect(screen.getAllByText('2 / 4 slots filled').length).toBeGreaterThanOrEqual(1);
  });

  it('shows golfer names', () => {
    render(
      <OpeningsList openings={[openOpening]} onCancel={vi.fn()} cancellingId={null} />,
    );

    expect(screen.getAllByText(/Alice Smith/).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Bob Jones/).length).toBeGreaterThanOrEqual(1);
  });

  it('shows cancel link only for Open openings', () => {
    render(
      <OpeningsList
        openings={[openOpening, filledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    // One Cancel per layout (mobile + desktop) for the single Open opening = 2 total
    const cancelLinks = screen.getAllByText('Cancel');
    expect(cancelLinks).toHaveLength(2);
  });

  it('shows summary line', () => {
    render(
      <OpeningsList
        openings={[openOpening, filledOpening]}
        onCancel={vi.fn()}
        cancellingId={null}
      />,
    );

    // 2 openings, filled: 2+4=6, total: 4+4=8
    expect(screen.getByText(/6\/8 filled/)).toBeInTheDocument();
  });
});
