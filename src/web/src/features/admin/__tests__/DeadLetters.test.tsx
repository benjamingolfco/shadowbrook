import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import DeadLetters from '../pages/DeadLetters';

vi.mock('../hooks/useDeadLetters');

import {
  useDeadLetters,
  useReplayDeadLetters,
  useDeleteDeadLetters,
} from '../hooks/useDeadLetters';

const mockUseDeadLetters = vi.mocked(useDeadLetters);
const mockUseReplayDeadLetters = vi.mocked(useReplayDeadLetters);
const mockUseDeleteDeadLetters = vi.mocked(useDeleteDeadLetters);

const noopMutation = {
  mutate: vi.fn(),
  isPending: false,
} as unknown as ReturnType<typeof useReplayDeadLetters>;

const sampleEnvelope = {
  id: 'msg-1',
  messageType: 'Teeforce.Domain.Events.BookingCreated',
  exceptionType: 'System.InvalidOperationException',
  exceptionMessage: 'Cannot book: tee time is full',
  sentAt: '2026-04-03T10:30:00Z',
  replayable: false,
  source: 'Teeforce.Api',
  receivedAt: 'local://queue/',
  message: { teeTimeId: 'tt-123', golferId: 'g-456' },
};

function wrapResponse(envelopes: typeof sampleEnvelope[]) {
  return [{ totalCount: envelopes.length, envelopes, pageNumber: 1, databaseUri: '' }];
}

beforeEach(() => {
  mockUseReplayDeadLetters.mockReturnValue(noopMutation);
  mockUseDeleteDeadLetters.mockReturnValue(noopMutation);
});

describe('DeadLetters', () => {
  it('shows loading state', () => {
    mockUseDeadLetters.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('Loading dead letter messages...')).toBeInTheDocument();
  });

  it('shows error state when fetch fails', () => {
    mockUseDeadLetters.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('Error: Network error')).toBeInTheDocument();
  });

  it('shows empty state when no envelopes', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('No dead letter messages. All clear.')).toBeInTheDocument();
  });

  it('renders envelope rows with stripped namespaces', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('BookingCreated')).toBeInTheDocument();
    expect(screen.getByText('InvalidOperationException')).toBeInTheDocument();
  });

  it('truncates long exception messages in the table', () => {
    const longMsg = 'A'.repeat(100);
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([{ ...sampleEnvelope, exceptionMessage: longMsg }]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText(/A{80}…/)).toBeInTheDocument();
  });

  it('expands row to show full exception message and body when clicked', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);

    expect(screen.queryByTestId('dead-letter-detail')).not.toBeInTheDocument();
    expect(screen.queryByText(/tt-123/)).not.toBeInTheDocument();

    const row = screen.getByText('BookingCreated').closest('tr');
    expect(row).not.toBeNull();
    fireEvent.click(row!);

    expect(screen.getByTestId('dead-letter-detail')).toBeInTheDocument();
    expect(screen.getByText(/tt-123/)).toBeInTheDocument();
  });

  it('shows select-all checkbox and individual checkboxes', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    const checkboxes = screen.getAllByRole('checkbox');
    expect(checkboxes).toHaveLength(2);
  });

  it('shows action buttons when messages are selected', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.queryByRole('button', { name: 'Replay' })).not.toBeInTheDocument();

    const [, messageCheckbox] = screen.getAllByRole('checkbox');
    fireEvent.click(messageCheckbox!);

    expect(screen.getByRole('button', { name: 'Replay' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
  });

  it('calls replay mutation with selected ids', () => {
    const mutate = vi.fn();
    mockUseReplayDeadLetters.mockReturnValue({
      mutate,
      isPending: false,
    } as unknown as ReturnType<typeof useReplayDeadLetters>);

    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);

    const [, messageCheckbox] = screen.getAllByRole('checkbox');
    fireEvent.click(messageCheckbox!);
    fireEvent.click(screen.getByRole('button', { name: 'Replay' }));

    expect(mutate).toHaveBeenCalledWith(['msg-1'], expect.any(Object));
  });

  it('shows total count suffix on the topbar title', () => {
    mockUseDeadLetters.mockReturnValue({
      data: wrapResponse([sampleEnvelope]),
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText(/·\s*1/)).toBeInTheDocument();
  });
});
