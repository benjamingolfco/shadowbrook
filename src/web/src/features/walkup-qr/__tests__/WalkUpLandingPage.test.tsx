import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import WalkUpLandingPage from '../pages/WalkUpLandingPage';

describe('WalkUpLandingPage', () => {
  it('redirects unconditionally to the join page', () => {
    render(<WalkUpLandingPage />);

    // Navigate replaces the route — no landing page content should be visible
    expect(screen.queryByText('Walk-Up Waitlist')).not.toBeInTheDocument();
    expect(screen.queryByText('Waitlist Closed')).not.toBeInTheDocument();
    expect(screen.queryByText('Code Expired')).not.toBeInTheDocument();
  });
});
