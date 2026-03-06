import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@/test/test-utils';
import JoinForm from '../components/JoinForm';
import { useJoinWaitlist } from '../hooks/useWalkupJoin';
import type { VerifyCodeResponse } from '@/types/waitlist';

vi.mock('../hooks/useWalkupJoin');

const mockUseJoinWaitlist = vi.mocked(useJoinWaitlist);

const mockMutate = vi.fn();

const verifyData: VerifyCodeResponse = {
  courseWaitlistId: 'wl-1',
  courseName: 'Pine Valley Golf Club',
  shortCode: '4827',
};

function defaultJoinMutation(overrides = {}) {
  mockUseJoinWaitlist.mockReturnValue({
    mutate: mockMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useJoinWaitlist>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultJoinMutation();
});

describe('JoinForm', () => {
  it('renders course name as heading', () => {
    render(<JoinForm verifyData={verifyData} onJoined={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Pine Valley Golf Club' })).toBeInTheDocument();
  });

  it('shows validation error for empty first name', async () => {
    render(<JoinForm verifyData={verifyData} onJoined={vi.fn()} />);

    fireEvent.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('First name is required')).toBeInTheDocument();
    });
  });

  it('shows validation error for empty last name', async () => {
    render(<JoinForm verifyData={verifyData} onJoined={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('First Name'), { target: { value: 'John' } });
    fireEvent.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('Last name is required')).toBeInTheDocument();
    });
  });

  it('shows validation error for short phone number', async () => {
    render(<JoinForm verifyData={verifyData} onJoined={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('First Name'), { target: { value: 'John' } });
    fireEvent.change(screen.getByLabelText('Last Name'), { target: { value: 'Smith' } });
    fireEvent.change(screen.getByLabelText('Phone Number'), { target: { value: '123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('Enter a valid phone number')).toBeInTheDocument();
    });
  });

  it('disables button during submission', () => {
    defaultJoinMutation({ isPending: true });

    render(<JoinForm verifyData={verifyData} onJoined={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Joining...' })).toBeDisabled();
  });

  it('calls onJoined on successful submit', async () => {
    const onJoined = vi.fn();
    const joinResult = { entryId: 'e-1', golferName: 'John Smith', position: 1, courseName: 'Pine Valley Golf Club' };

    mockUseJoinWaitlist.mockReturnValue({
      mutate: vi.fn((_, options) => options?.onSuccess?.(joinResult)),
      isPending: false,
      isError: false,
      isSuccess: false,
      error: null,
    } as unknown as ReturnType<typeof useJoinWaitlist>);

    render(<JoinForm verifyData={verifyData} onJoined={onJoined} />);

    fireEvent.change(screen.getByLabelText('First Name'), { target: { value: 'John' } });
    fireEvent.change(screen.getByLabelText('Last Name'), { target: { value: 'Smith' } });
    fireEvent.change(screen.getByLabelText('Phone Number'), { target: { value: '5551234567' } });
    fireEvent.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(onJoined).toHaveBeenCalledWith(joinResult);
    });
  });

  it('treats 409 duplicate as success and calls onJoined with position', async () => {
    const onJoined = vi.fn();
    const error = Object.assign(new Error("You're already on the waitlist."), {
      status: 409,
      data: { error: "You're already on the waitlist.", position: 2 },
    });

    mockUseJoinWaitlist.mockReturnValue({
      mutate: vi.fn((_, options) => options?.onError?.(error)),
      isPending: false,
      isError: false,
      isSuccess: false,
      error: null,
    } as unknown as ReturnType<typeof useJoinWaitlist>);

    render(<JoinForm verifyData={verifyData} onJoined={onJoined} />);

    fireEvent.change(screen.getByLabelText('First Name'), { target: { value: 'John' } });
    fireEvent.change(screen.getByLabelText('Last Name'), { target: { value: 'Smith' } });
    fireEvent.change(screen.getByLabelText('Phone Number'), { target: { value: '5551234567' } });
    fireEvent.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(onJoined).toHaveBeenCalledWith(
        expect.objectContaining({ position: 2, courseName: 'Pine Valley Golf Club' }),
      );
    });
  });

  it('shows error message on generic failure', () => {
    const error = Object.assign(new Error('Something went wrong.'), { status: 500 });
    defaultJoinMutation({ isError: true, error });

    render(<JoinForm verifyData={verifyData} onJoined={vi.fn()} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong.');
  });
});
