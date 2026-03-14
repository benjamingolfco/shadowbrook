import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import CountdownTimer from '../components/CountdownTimer';

describe('CountdownTimer', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('displays remaining time correctly', () => {
    const expiresAt = new Date(Date.now() + 10 * 60 * 1000).toISOString(); // 10 minutes from now
    const onExpired = vi.fn();

    render(<CountdownTimer expiresAt={expiresAt} onExpired={onExpired} />);

    expect(screen.getByRole('timer')).toBeInTheDocument();
    expect(screen.getByRole('timer')).toHaveTextContent(/10 minutes remaining/);
  });

  it('calls onExpired when countdown reaches zero', () => {
    const expiresAt = new Date(Date.now() + 2000).toISOString(); // 2 seconds from now
    const onExpired = vi.fn();

    render(<CountdownTimer expiresAt={expiresAt} onExpired={onExpired} />);

    expect(onExpired).not.toHaveBeenCalled();

    // Advance time by 2 seconds
    vi.advanceTimersByTime(2000);

    expect(onExpired).toHaveBeenCalledTimes(1);
  });

  it('has correct ARIA attributes', () => {
    const expiresAt = new Date(Date.now() + 5 * 60 * 1000).toISOString(); // 5 minutes from now
    const onExpired = vi.fn();

    render(<CountdownTimer expiresAt={expiresAt} onExpired={onExpired} />);

    const timer = screen.getByRole('timer');
    expect(timer).toHaveAttribute('aria-live', 'polite');
    expect(timer).toHaveAttribute('aria-atomic', 'true');
  });
});
