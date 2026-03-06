import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import { PageHeader } from '../PageHeader';
import type { PageAction } from '../PageHeader';

describe('PageHeader', () => {
  it('renders the title', () => {
    render(<PageHeader title="Walk-Up Waitlist" />);
    expect(screen.getByRole('heading', { name: 'Walk-Up Waitlist' })).toBeInTheDocument();
  });

  it('renders children in the subtitle area', () => {
    render(
      <PageHeader title="Walk-Up Waitlist">
        <span>Short Code: 4827</span>
      </PageHeader>,
    );
    expect(screen.getByText('Short Code: 4827')).toBeInTheDocument();
  });

  it('renders action buttons from PageAction array', () => {
    const actions: PageAction[] = [
      { id: 'open', label: 'Open Waitlist', onClick: vi.fn() },
    ];
    render(<PageHeader title="Walk-Up Waitlist" actions={actions} />);
    expect(screen.getByRole('button', { name: 'Open Waitlist' })).toBeInTheDocument();
  });

  it('calls onClick when action button is clicked', () => {
    const handleClick = vi.fn();
    const actions: PageAction[] = [
      { id: 'open', label: 'Open Waitlist', onClick: handleClick },
    ];
    render(<PageHeader title="Walk-Up Waitlist" actions={actions} />);
    fireEvent.click(screen.getByRole('button', { name: 'Open Waitlist' }));
    expect(handleClick).toHaveBeenCalledOnce();
  });

  it('renders multiple actions with correct variants', () => {
    const actions: PageAction[] = [
      { id: 'add', label: 'Add Request', variant: 'outline', onClick: vi.fn() },
      { id: 'close', label: 'Close Waitlist', variant: 'destructive', onClick: vi.fn() },
    ];
    render(<PageHeader title="Walk-Up Waitlist" actions={actions} />);
    expect(screen.getByRole('button', { name: 'Add Request' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Close Waitlist' })).toBeInTheDocument();
  });

  it('shows disabledLabel when action is disabled', () => {
    const actions: PageAction[] = [
      { id: 'open', label: 'Open Waitlist', onClick: vi.fn(), disabled: true, disabledLabel: 'Opening...' },
    ];
    render(<PageHeader title="Walk-Up Waitlist" actions={actions} />);
    const button = screen.getByRole('button', { name: 'Opening...' });
    expect(button).toBeDisabled();
  });

  it('sets title attribute from description', () => {
    const actions: PageAction[] = [
      { id: 'open', label: 'Open Waitlist', description: 'Opens the waitlist for today', onClick: vi.fn() },
    ];
    render(<PageHeader title="Walk-Up Waitlist" actions={actions} />);
    expect(screen.getByRole('button', { name: 'Open Waitlist' })).toHaveAttribute('title', 'Opens the waitlist for today');
  });

  it('renders title, children, and actions together', () => {
    const actions: PageAction[] = [
      { id: 'action', label: 'Action', onClick: vi.fn() },
    ];
    render(
      <PageHeader title="Walk-Up Waitlist" actions={actions}>
        <span>subtitle</span>
      </PageHeader>,
    );
    expect(screen.getByRole('heading', { name: 'Walk-Up Waitlist' })).toBeInTheDocument();
    expect(screen.getByText('subtitle')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Action' })).toBeInTheDocument();
  });
});
