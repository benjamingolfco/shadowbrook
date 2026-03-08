import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import CodeEntry from '../components/CodeEntry';
import { useVerifyCode } from '../hooks/useWalkupJoin';

vi.mock('../hooks/useWalkupJoin');

const mockUseVerifyCode = vi.mocked(useVerifyCode);

const mockMutate = vi.fn();

function defaultVerifyMutation(overrides = {}) {
  mockUseVerifyCode.mockReturnValue({
    mutate: mockMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    ...overrides,
  } as unknown as ReturnType<typeof useVerifyCode>);
}

beforeEach(() => {
  vi.clearAllMocks();
  defaultVerifyMutation();
});

describe('CodeEntry', () => {
  it('renders input with numeric inputMode', () => {
    render(<CodeEntry onVerified={vi.fn()} />);

    const input = screen.getByRole('textbox');
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('inputMode', 'numeric');
  });

  it('renders helper text', () => {
    render(<CodeEntry onVerified={vi.fn()} />);

    expect(screen.getByText('Enter the 4-digit code posted at the course')).toBeInTheDocument();
  });

  it('does not submit with fewer than 4 digits', () => {
    render(<CodeEntry onVerified={vi.fn()} />);

    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: '123' } });

    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('auto-submits on 4th digit', () => {
    render(<CodeEntry onVerified={vi.fn()} />);

    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: '4827' } });

    expect(mockMutate).toHaveBeenCalledWith('4827', expect.any(Object));
  });

  it('strips non-numeric characters', () => {
    render(<CodeEntry onVerified={vi.fn()} />);

    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: 'ab12cd' } });

    expect(input).toHaveValue('12');
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('shows loading state during verification', () => {
    defaultVerifyMutation({ isPending: true });

    render(<CodeEntry onVerified={vi.fn()} />);

    expect(screen.getByText('Verifying...')).toBeInTheDocument();
    expect(screen.getByRole('textbox')).toBeDisabled();
  });

  it('shows error message on 404 response', () => {
    const error = Object.assign(new Error('Code not found or waitlist is not active.'), { status: 404 });
    defaultVerifyMutation({ isError: true, error });

    render(<CodeEntry onVerified={vi.fn()} />);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Code not found. Check the code posted at the course and try again.',
    );
  });

  it('shows generic error message on non-404 error', () => {
    const error = Object.assign(new Error('Internal Server Error'), { status: 500 });
    defaultVerifyMutation({ isError: true, error });

    render(<CodeEntry onVerified={vi.fn()} />);

    expect(screen.getByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
  });

  it('calls onVerified when verification succeeds', () => {
    const onVerified = vi.fn();
    const verifyData = { courseWaitlistId: 'wl-1', courseName: 'Pine Valley', shortCode: '4827' };

    mockUseVerifyCode.mockReturnValue({
      mutate: vi.fn((_, options) => options?.onSuccess?.(verifyData)),
      isPending: false,
      isError: false,
      isSuccess: false,
      error: null,
    } as unknown as ReturnType<typeof useVerifyCode>);

    render(<CodeEntry onVerified={onVerified} />);

    const input = screen.getByRole('textbox');
    fireEvent.change(input, { target: { value: '4827' } });

    expect(onVerified).toHaveBeenCalledWith(verifyData);
  });
});
