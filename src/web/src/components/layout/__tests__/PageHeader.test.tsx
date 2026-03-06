import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import { PageHeader } from '../PageHeader';

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

  it('renders actions', () => {
    render(
      <PageHeader
        title="Walk-Up Waitlist"
        actions={<button>Open Waitlist</button>}
      />,
    );
    expect(screen.getByRole('button', { name: 'Open Waitlist' })).toBeInTheDocument();
  });

  it('renders title, children, and actions together', () => {
    render(
      <PageHeader
        title="Walk-Up Waitlist"
        actions={<button>Action</button>}
      >
        <span>subtitle</span>
      </PageHeader>,
    );
    expect(screen.getByRole('heading', { name: 'Walk-Up Waitlist' })).toBeInTheDocument();
    expect(screen.getByText('subtitle')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Action' })).toBeInTheDocument();
  });
});
