import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import { UnpublishTeeSheetDialog } from '../manage/components/UnpublishTeeSheetDialog';

describe('UnpublishTeeSheetDialog', () => {
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    onConfirm: vi.fn(),
    isPending: false,
    bookingCount: 3,
  };

  it('shows booking count in description', () => {
    render(<UnpublishTeeSheetDialog {...defaultProps} />);

    expect(
      screen.getByText(/3 booking\(s\) will be cancelled/),
    ).toBeInTheDocument();
  });

  it('shows reason textarea', () => {
    render(<UnpublishTeeSheetDialog {...defaultProps} />);

    expect(screen.getByLabelText('Reason (included in cancellation SMS)')).toBeInTheDocument();
  });

  it('calls onConfirm with reason when Unpublish is clicked', () => {
    render(<UnpublishTeeSheetDialog {...defaultProps} />);

    const textarea = screen.getByLabelText('Reason (included in cancellation SMS)');
    fireEvent.change(textarea, { target: { value: 'Weather cancellation' } });
    fireEvent.click(screen.getByRole('button', { name: 'Unpublish' }));

    expect(defaultProps.onConfirm).toHaveBeenCalledWith('Weather cancellation');
  });

  it('calls onConfirm with undefined when reason is empty', () => {
    render(<UnpublishTeeSheetDialog {...defaultProps} />);

    fireEvent.click(screen.getByRole('button', { name: 'Unpublish' }));

    expect(defaultProps.onConfirm).toHaveBeenCalledWith(undefined);
  });

  it('disables buttons when isPending is true', () => {
    render(<UnpublishTeeSheetDialog {...defaultProps} isPending={true} />);

    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Unpublish' })).toBeDisabled();
  });

  it('resets reason when dialog closes', () => {
    const { rerender } = render(<UnpublishTeeSheetDialog {...defaultProps} />);

    const textarea = screen.getByLabelText('Reason (included in cancellation SMS)');
    fireEvent.change(textarea, { target: { value: 'Some reason' } });

    // Close dialog
    rerender(<UnpublishTeeSheetDialog {...defaultProps} open={false} />);
    // Reopen dialog
    rerender(<UnpublishTeeSheetDialog {...defaultProps} open={true} />);

    const newTextarea = screen.getByLabelText('Reason (included in cancellation SMS)');
    expect(newTextarea).toHaveValue('');
  });
});
