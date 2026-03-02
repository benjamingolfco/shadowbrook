import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@/test/test-utils';
import { userEvent } from '@testing-library/user-event';
import JoinForm from '../components/JoinForm';
import { useJoinWaitlist } from '../hooks/useJoinWaitlist';

vi.mock('../hooks/useJoinWaitlist');

const mockUseJoinWaitlist = vi.mocked(useJoinWaitlist);
const mockMutate = vi.fn();

const defaultProps = {
  courseWaitlistId: 'waitlist-id-123',
  courseName: 'Pine Valley Golf Club',
  onJoined: vi.fn(),
};

beforeEach(() => {
  vi.clearAllMocks();
  mockUseJoinWaitlist.mockReturnValue({
    mutate: mockMutate,
    isPending: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useJoinWaitlist>);
});

describe('JoinForm', () => {
  it('renders course name as heading', () => {
    render(<JoinForm {...defaultProps} />);
    expect(screen.getByRole('heading', { name: 'Pine Valley Golf Club' })).toBeInTheDocument();
  });

  it('renders first name, last name, and phone fields', () => {
    render(<JoinForm {...defaultProps} />);
    expect(screen.getByLabelText('First Name')).toBeInTheDocument();
    expect(screen.getByLabelText('Last Name')).toBeInTheDocument();
    expect(screen.getByLabelText('Phone Number')).toBeInTheDocument();
  });

  it('shows validation error for empty first name', async () => {
    const user = userEvent.setup();
    render(<JoinForm {...defaultProps} />);

    await user.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('First name is required')).toBeInTheDocument();
    });
  });

  it('shows validation error for empty last name', async () => {
    const user = userEvent.setup();
    render(<JoinForm {...defaultProps} />);

    await user.type(screen.getByLabelText('First Name'), 'Tiger');
    await user.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('Last name is required')).toBeInTheDocument();
    });
  });

  it('shows validation error for invalid phone', async () => {
    const user = userEvent.setup();
    render(<JoinForm {...defaultProps} />);

    await user.type(screen.getByLabelText('First Name'), 'Tiger');
    await user.type(screen.getByLabelText('Last Name'), 'Woods');
    await user.type(screen.getByLabelText('Phone Number'), 'abc');
    await user.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(screen.getByText('Please enter a valid phone number')).toBeInTheDocument();
    });
  });

  it('submits form with valid data', async () => {
    const user = userEvent.setup();
    render(<JoinForm {...defaultProps} />);

    await user.type(screen.getByLabelText('First Name'), 'Tiger');
    await user.type(screen.getByLabelText('Last Name'), 'Woods');
    await user.type(screen.getByLabelText('Phone Number'), '(612) 555-1234');
    await user.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    await waitFor(() => {
      expect(mockMutate).toHaveBeenCalledWith(
        {
          courseWaitlistId: 'waitlist-id-123',
          firstName: 'Tiger',
          lastName: 'Woods',
          phone: '(612) 555-1234',
        },
        expect.any(Object),
      );
    });
  });

  it('shows loading state during submission', () => {
    mockUseJoinWaitlist.mockReturnValue({
      mutate: mockMutate,
      isPending: true,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useJoinWaitlist>);

    render(<JoinForm {...defaultProps} />);

    const button = screen.getByRole('button', { name: 'Joining...' });
    expect(button).toBeInTheDocument();
    expect(button).toBeDisabled();
  });
});
