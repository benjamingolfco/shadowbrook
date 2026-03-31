import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorLayout from '@/components/layout/OperatorLayout';
import { useAuth } from '@/features/auth/hooks/useAuth';

vi.mock('@/features/auth/hooks/useAuth');
vi.mock('@/features/operator/components/CourseSwitcher', () => ({
  default: () => <div data-testid="course-switcher">CourseSwitcher Mock</div>,
}));

const mockUseAuth = vi.mocked(useAuth);

describe('OperatorLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Owner',
        organization: { id: 'org-1', name: 'Pine Valley Golf Club' },
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      permissions: ['app:access'],
      courses: [],
      login: vi.fn(),
      logout: vi.fn(),
      hasPermission: vi.fn(() => true),
    });
  });

  it('shows organization name in sidebar header', () => {
    render(<OperatorLayout />);
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
  });

  it('shows Shadowbrook when user has no organization', () => {
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Staff',
        organization: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      permissions: ['app:access'],
      courses: [],
      login: vi.fn(),
      logout: vi.fn(),
      hasPermission: vi.fn(() => true),
    });

    render(<OperatorLayout />);
    expect(screen.getByText('Shadowbrook')).toBeInTheDocument();
  });

  it('applies truncate class and title attribute for long organization names', () => {
    const longName = 'Very Long Organization Name That Should Be Truncated';
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Owner',
        organization: { id: 'org-1', name: longName },
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      permissions: ['app:access'],
      courses: [],
      login: vi.fn(),
      logout: vi.fn(),
      hasPermission: vi.fn(() => true),
    });

    render(<OperatorLayout />);
    const heading = screen.getByText(longName);
    expect(heading).toHaveClass('truncate');
    expect(heading).toHaveClass('max-w-[180px]');
    expect(heading).toHaveAttribute('title', longName);
  });

  it('renders CourseSwitcher in sidebar header', () => {
    render(<OperatorLayout />);
    expect(screen.getByTestId('course-switcher')).toBeInTheDocument();
  });
});
