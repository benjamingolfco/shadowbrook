import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorLayout from '@/components/layout/OperatorLayout';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from '@/features/operator/context/CourseContext';

vi.mock('@/features/auth/hooks/useAuth');
vi.mock('@/features/operator/context/CourseContext');

const mockUseAuth = vi.mocked(useAuth);
const mockUseCourseContext = vi.mocked(useCourseContext);

describe('OperatorLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseCourseContext.mockReturnValue({
      course: null,
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
      isDirty: false,
      registerDirtyForm: vi.fn(),
      unregisterDirtyForm: vi.fn(),
    });
    mockUseAuth.mockReturnValue({
      user: {
        id: '1',
        email: 'test@test.com',
        displayName: 'Test User',
        role: 'Operator',
        organization: { id: 'org-1', name: 'Pine Valley Golf Club' },
        organizations: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      unauthorized: false,
      permissions: ['app:access'],
      courses: [],
      organizations: [],
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
        role: 'Operator',
        organization: null,
        organizations: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      unauthorized: false,
      permissions: ['app:access'],
      courses: [],
      organizations: [],
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
        role: 'Operator',
        organization: { id: 'org-1', name: longName },
        organizations: null,
        courses: [],
        permissions: ['app:access'],
      },
      isAuthenticated: true,
      isLoading: false,
      unauthorized: false,
      permissions: ['app:access'],
      courses: [],
      organizations: [],
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

});
