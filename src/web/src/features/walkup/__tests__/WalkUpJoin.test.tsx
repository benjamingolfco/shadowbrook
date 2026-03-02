import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@/test/test-utils';
import { userEvent } from '@testing-library/user-event';
import WalkUpJoin from '../pages/WalkUpJoin';
import { useVerifyCode } from '../hooks/useVerifyCode';
import { useJoinWaitlist } from '../hooks/useJoinWaitlist';

vi.mock('../hooks/useVerifyCode');
vi.mock('../hooks/useJoinWaitlist');

const mockUseVerifyCode = vi.mocked(useVerifyCode);
const mockUseJoinWaitlist = vi.mocked(useJoinWaitlist);

const mockVerifyMutate = vi.fn();
const mockJoinMutate = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();

  mockUseVerifyCode.mockReturnValue({
    mutate: mockVerifyMutate,
    isPending: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useVerifyCode>);

  mockUseJoinWaitlist.mockReturnValue({
    mutate: mockJoinMutate,
    isPending: false,
    isError: false,
    error: null,
  } as unknown as ReturnType<typeof useJoinWaitlist>);
});

describe('WalkUpJoin', () => {
  it('initially renders code entry phase', () => {
    render(<WalkUpJoin />);

    expect(screen.getByText('Shadowbrook')).toBeInTheDocument();
    expect(screen.getByLabelText('Enter the 4-digit code')).toBeInTheDocument();
    expect(screen.queryByLabelText('First Name')).not.toBeInTheDocument();
    expect(screen.queryByText("You're on the list")).not.toBeInTheDocument();
  });

  it('transitions to join form after code verification', async () => {
    const user = userEvent.setup();

    mockVerifyMutate.mockImplementation(
      (_code: string, { onSuccess }: { onSuccess: (data: { courseWaitlistId: string; courseId: string; courseName: string; date: string }) => void }) => {
        onSuccess({
          courseWaitlistId: 'waitlist-abc',
          courseId: 'course-abc',
          courseName: 'Pine Valley Golf Club',
          date: '2026-03-02',
        });
      },
    );

    render(<WalkUpJoin />);

    const codeInput = screen.getByLabelText('Enter the 4-digit code');
    await user.type(codeInput, '4829');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: 'Pine Valley Golf Club' })).toBeInTheDocument();
      expect(screen.getByLabelText('First Name')).toBeInTheDocument();
    });

    expect(screen.queryByLabelText('Enter the 4-digit code')).not.toBeInTheDocument();
  });

  it('transitions to confirmation after joining', async () => {
    const user = userEvent.setup();

    mockVerifyMutate.mockImplementation(
      (_code: string, { onSuccess }: { onSuccess: (data: { courseWaitlistId: string; courseId: string; courseName: string; date: string }) => void }) => {
        onSuccess({
          courseWaitlistId: 'waitlist-abc',
          courseId: 'course-abc',
          courseName: 'Pine Valley Golf Club',
          date: '2026-03-02',
        });
      },
    );

    mockJoinMutate.mockImplementation(
      (_data: unknown, { onSuccess }: { onSuccess: (response: { entryId: string; firstName: string; position: number; isExisting: boolean }) => void }) => {
        onSuccess({
          entryId: 'entry-123',
          firstName: 'Tiger',
          position: 1,
          isExisting: false,
        });
      },
    );

    render(<WalkUpJoin />);

    // Phase 1: Enter code
    await user.type(screen.getByLabelText('Enter the 4-digit code'), '4829');

    // Phase 2: Fill form
    await waitFor(() => {
      expect(screen.getByLabelText('First Name')).toBeInTheDocument();
    });

    await user.type(screen.getByLabelText('First Name'), 'Tiger');
    await user.type(screen.getByLabelText('Last Name'), 'Woods');
    await user.type(screen.getByLabelText('Phone Number'), '(612) 555-1234');
    await user.click(screen.getByRole('button', { name: 'Join Waitlist' }));

    // Phase 3: Confirmation
    await waitFor(() => {
      expect(screen.getByText("You're on the list, Tiger!")).toBeInTheDocument();
      expect(screen.getByText('Your position: #1')).toBeInTheDocument();
    });

    expect(screen.queryByLabelText('First Name')).not.toBeInTheDocument();
  });
});
