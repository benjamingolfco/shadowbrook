import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@/test/test-utils';
import { userEvent } from '@testing-library/user-event';
import CodeEntry from '../components/CodeEntry';
import { useVerifyCode } from '../hooks/useVerifyCode';

vi.mock('../hooks/useVerifyCode');

const mockUseVerifyCode = vi.mocked(useVerifyCode);
const mockMutate = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  mockUseVerifyCode.mockReturnValue({
    mutate: mockMutate,
    isPending: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useVerifyCode>);
});

describe('CodeEntry', () => {
  it('renders code input with label', () => {
    const onVerified = vi.fn();
    render(<CodeEntry onVerified={onVerified} />);

    expect(screen.getByLabelText('Enter the 4-digit code')).toBeInTheDocument();
    expect(screen.getByText(/starter's booth or pro shop/)).toBeInTheDocument();
  });

  it('auto-submits when 4th digit is entered', async () => {
    const user = userEvent.setup();
    const onVerified = vi.fn();

    render(<CodeEntry onVerified={onVerified} />);

    const input = screen.getByLabelText('Enter the 4-digit code');
    await user.type(input, '1234');

    expect(mockMutate).toHaveBeenCalledWith('1234', expect.any(Object));
  });

  it('shows error message on 404', async () => {
    const user = userEvent.setup();
    const onVerified = vi.fn();

    mockMutate.mockImplementation((_code: string, { onError }: { onError: (err: Error) => void }) => {
      const err = Object.assign(new Error('Code not found'), { status: 404 });
      onError(err);
    });

    render(<CodeEntry onVerified={onVerified} />);

    const input = screen.getByLabelText('Enter the 4-digit code');
    await user.type(input, '9999');

    await waitFor(() => {
      expect(
        screen.getByText('Code not found. Check the code posted at the course and try again.'),
      ).toBeInTheDocument();
    });
  });

  it('shows expired message on 410', async () => {
    const user = userEvent.setup();
    const onVerified = vi.fn();

    mockMutate.mockImplementation((_code: string, { onError }: { onError: (err: Error) => void }) => {
      const err = Object.assign(new Error('Code expired'), { status: 410 });
      onError(err);
    });

    render(<CodeEntry onVerified={onVerified} />);

    const input = screen.getByLabelText('Enter the 4-digit code');
    await user.type(input, '0000');

    await waitFor(() => {
      expect(
        screen.getByText("This waitlist code has expired. Ask the pro shop for today's code."),
      ).toBeInTheDocument();
    });
  });

  it('clears input on error', async () => {
    const user = userEvent.setup();
    const onVerified = vi.fn();

    mockMutate.mockImplementation((_code: string, { onError }: { onError: (err: Error) => void }) => {
      const err = Object.assign(new Error('Code not found'), { status: 404 });
      onError(err);
    });

    render(<CodeEntry onVerified={onVerified} />);

    const input = screen.getByLabelText('Enter the 4-digit code') as HTMLInputElement;
    await user.type(input, '1234');

    await waitFor(() => {
      expect(input.value).toBe('');
    });
  });

  it('shows loading state during verification', () => {
    const onVerified = vi.fn();

    mockUseVerifyCode.mockReturnValue({
      mutate: mockMutate,
      isPending: true,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useVerifyCode>);

    render(<CodeEntry onVerified={onVerified} />);

    expect(screen.getByText('Verifying...')).toBeInTheDocument();
    expect(screen.getByLabelText('Enter the 4-digit code')).toBeDisabled();
  });
});
