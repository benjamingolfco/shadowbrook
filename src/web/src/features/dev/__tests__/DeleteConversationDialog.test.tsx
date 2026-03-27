import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import { DeleteConversationDialog } from '../components/DeleteConversationDialog';

describe('DeleteConversationDialog', () => {
  const mockOnOpenChange = vi.fn();
  const mockOnConfirm = vi.fn();
  const testPhoneNumber = '+15551234567';

  it('renders with phone number in description', () => {
    render(
      <DeleteConversationDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        onConfirm={mockOnConfirm}
        phoneNumber={testPhoneNumber}
        isPending={false}
      />
    );

    expect(screen.getByText('Delete Conversation?')).toBeInTheDocument();
    expect(screen.getByText(/Delete all messages for \+15551234567\?/)).toBeInTheDocument();
  });

  it('calls onConfirm when Delete button is clicked', () => {
    render(
      <DeleteConversationDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        onConfirm={mockOnConfirm}
        phoneNumber={testPhoneNumber}
        isPending={false}
      />
    );

    const deleteButton = screen.getByRole('button', { name: 'Delete' });
    fireEvent.click(deleteButton);

    expect(mockOnConfirm).toHaveBeenCalledTimes(1);
  });

  it('calls onOpenChange with false when Cancel button is clicked', () => {
    render(
      <DeleteConversationDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        onConfirm={mockOnConfirm}
        phoneNumber={testPhoneNumber}
        isPending={false}
      />
    );

    const cancelButton = screen.getByRole('button', { name: 'Cancel' });
    fireEvent.click(cancelButton);

    expect(mockOnOpenChange).toHaveBeenCalledWith(false);
  });

  it('disables buttons when isPending is true', () => {
    render(
      <DeleteConversationDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        onConfirm={mockOnConfirm}
        phoneNumber={testPhoneNumber}
        isPending={true}
      />
    );

    const deleteButton = screen.getByRole('button', { name: 'Delete' });
    const cancelButton = screen.getByRole('button', { name: 'Cancel' });

    expect(deleteButton).toBeDisabled();
    expect(cancelButton).toBeDisabled();
  });

  it('applies destructive styling to Delete button', () => {
    render(
      <DeleteConversationDialog
        open={true}
        onOpenChange={mockOnOpenChange}
        onConfirm={mockOnConfirm}
        phoneNumber={testPhoneNumber}
        isPending={false}
      />
    );

    const deleteButton = screen.getByRole('button', { name: 'Delete' });
    expect(deleteButton).toHaveClass('bg-destructive');
  });

  it('does not render when open is false', () => {
    render(
      <DeleteConversationDialog
        open={false}
        onOpenChange={mockOnOpenChange}
        onConfirm={mockOnConfirm}
        phoneNumber={testPhoneNumber}
        isPending={false}
      />
    );

    expect(screen.queryByText('Delete Conversation?')).not.toBeInTheDocument();
  });
});
