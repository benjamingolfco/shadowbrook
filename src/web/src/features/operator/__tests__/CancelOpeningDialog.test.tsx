import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import { CancelOpeningDialog } from '../components/CancelOpeningDialog';

describe('CancelOpeningDialog', () => {
  it('renders confirmation text', () => {
    render(
      <CancelOpeningDialog
        open={true}
        onOpenChange={vi.fn()}
        onConfirm={vi.fn()}
        isPending={false}
      />
    );

    expect(screen.getByText('Cancel Tee Time Opening?')).toBeInTheDocument();
    expect(
      screen.getByText(/Cancel this tee time opening\? Any pending offers will be withdrawn/)
    ).toBeInTheDocument();
    expect(screen.getByText('Keep Opening')).toBeInTheDocument();
    expect(screen.getByText('Cancel Opening')).toBeInTheDocument();
  });

  it('calls onConfirm when confirm button is clicked', () => {
    const mockOnConfirm = vi.fn();
    render(
      <CancelOpeningDialog
        open={true}
        onOpenChange={vi.fn()}
        onConfirm={mockOnConfirm}
        isPending={false}
      />
    );

    fireEvent.click(screen.getByText('Cancel Opening'));
    expect(mockOnConfirm).toHaveBeenCalledTimes(1);
  });

  it('calls onOpenChange when cancel button is clicked', () => {
    const mockOnOpenChange = vi.fn();
    render(
      <CancelOpeningDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        onConfirm={vi.fn()}
        isPending={false}
      />
    );

    fireEvent.click(screen.getByText('Keep Opening'));
    expect(mockOnOpenChange).toHaveBeenCalledWith(false);
  });

  it('disables buttons when isPending is true', () => {
    render(
      <CancelOpeningDialog
        open={true}
        onOpenChange={vi.fn()}
        onConfirm={vi.fn()}
        isPending={true}
      />
    );

    expect(screen.getByText('Keep Opening')).toBeDisabled();
    expect(screen.getByText('Cancel Opening')).toBeDisabled();
  });
});
