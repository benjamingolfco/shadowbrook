import { describe, it, expect } from 'vitest';
import { render, screen } from '@/test/test-utils';
import Confirmation from '../components/Confirmation';

describe('Confirmation', () => {
  it('shows personalized confirmation message', () => {
    render(<Confirmation firstName="Tiger" position={3} isExisting={false} />);
    expect(screen.getByText("You're on the list, Tiger!")).toBeInTheDocument();
  });

  it('shows queue position', () => {
    render(<Confirmation firstName="Tiger" position={3} isExisting={false} />);
    expect(screen.getByText('Your position: #3')).toBeInTheDocument();
  });

  it('shows existing message when isExisting is true', () => {
    render(<Confirmation firstName="Tiger" position={2} isExisting={true} />);
    expect(screen.getByText('You were already on the list.')).toBeInTheDocument();
  });

  it('shows keep-your-phone-handy subtext', () => {
    render(<Confirmation firstName="Tiger" position={1} isExisting={false} />);
    expect(
      screen.getByText(/Keep your phone handy/),
    ).toBeInTheDocument();
  });
});
