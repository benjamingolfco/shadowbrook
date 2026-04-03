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

const sampleMessage = {
  Id: 'msg-1',
  MessageType: 'Shadowbrook.Domain.Events.BookingCreated',
  ExceptionType: 'System.InvalidOperationException',
  ExceptionMessage: 'Cannot book: tee time is full',
  SentAt: '2026-04-03T10:30:00Z',
  Body: { TeeTimeId: 'tt-123', GolferId: 'g-456' },
};

beforeEach(() => {
  mockUseReplayDeadLetters.mockReturnValue(noopMutation);
  mockUseDeleteDeadLetters.mockReturnValue(noopMutation);
});

describe('DeadLetters', () => {
  it('shows loading state when no messages loaded yet', () => {
    mockUseDeadLetters.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('Loading dead letter messages...')).toBeInTheDocument();
  });

  it('shows error state when fetch fails and no messages loaded', () => {
    mockUseDeadLetters.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('Error: Network error')).toBeInTheDocument();
  });

  it('shows empty state when no messages', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [], NextId: null },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText('No dead letter messages. All clear.')).toBeInTheDocument();
  });

  it('renders message rows with stripped namespaces', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [sampleMessage], NextId: null },
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
      data: {
        Messages: [{ ...sampleMessage, ExceptionMessage: longMsg }],
        NextId: null,
      },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByText(/A{80}…/)).toBeInTheDocument();
  });

  it('expands row to show full exception message and body when clicked', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [sampleMessage], NextId: null },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);

    // Expanded detail panel not visible yet
    expect(screen.queryByTestId('dead-letter-detail')).not.toBeInTheDocument();
    expect(screen.queryByText(/tt-123/)).not.toBeInTheDocument();

    // Click the row (not the checkbox cell)
    const row = screen.getByText('BookingCreated').closest('tr');
    expect(row).not.toBeNull();
    fireEvent.click(row!);

    // Expanded detail panel should now appear with full body content
    expect(screen.getByTestId('dead-letter-detail')).toBeInTheDocument();
    expect(screen.getByText(/tt-123/)).toBeInTheDocument();
  });

  it('shows select-all checkbox and individual checkboxes', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [sampleMessage], NextId: null },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);

    const checkboxes = screen.getAllByRole('checkbox');
    // One select-all + one per message
    expect(checkboxes).toHaveLength(2);
  });

  it('shows action buttons when messages are selected', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [sampleMessage], NextId: null },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);

    // No buttons initially
    expect(screen.queryByRole('button', { name: 'Replay' })).not.toBeInTheDocument();

    // Select the message
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
      data: { Messages: [sampleMessage], NextId: null },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);

    const [, messageCheckbox] = screen.getAllByRole('checkbox');
    fireEvent.click(messageCheckbox!);
    fireEvent.click(screen.getByRole('button', { name: 'Replay' }));

    expect(mutate).toHaveBeenCalledWith(['msg-1'], expect.any(Object));
  });

  it('shows Load more button when NextId is present', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [sampleMessage], NextId: 'next-cursor-guid' },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.getByRole('button', { name: 'Load more' })).toBeInTheDocument();
  });

  it('does not show Load more button when NextId is null', () => {
    mockUseDeadLetters.mockReturnValue({
      data: { Messages: [sampleMessage], NextId: null },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useDeadLetters>);

    render(<DeadLetters />);
    expect(screen.queryByRole('button', { name: 'Load more' })).not.toBeInTheDocument();
  });
});
