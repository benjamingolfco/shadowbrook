import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorFeature from '../index';

vi.mock('@/hooks/use-features', () => ({
  useFeature: vi.fn(),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: vi.fn(() => ({
    user: { displayName: 'Test User', email: 'test@test.com', organization: { name: 'Test Org' } },
    logout: vi.fn(),
    courses: [{ id: 'course-1', name: 'Test Course' }],
  })),
}));

vi.mock('../context/CourseContext', () => ({
  CourseProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useCourseContext: vi.fn(() => ({
    course: { id: 'course-1', name: 'Test Course', timeZoneId: 'America/New_York' },
    selectCourse: vi.fn(),
    clearCourse: vi.fn(),
    isDirty: false,
    registerDirtyForm: vi.fn(),
    unregisterDirtyForm: vi.fn(),
  })),
}));

vi.mock('@/components/ThemeProvider', () => ({
  ThemeProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

vi.mock('../pages/WalkUpWaitlist', () => ({
  default: () => <div data-testid="waitlist-page">Waitlist</div>,
}));

vi.mock('../pages/TeeSheet', () => ({
  default: () => <div data-testid="tee-sheet-page">Tee Sheet</div>,
}));

// Mock the Sidebar-heavy OperatorLayout with a simple pass-through
vi.mock('@/components/layout/OperatorLayout', () => ({
  default: () => <div data-testid="operator-layout" />,
}));

import { useFeature } from '@/hooks/use-features';

const mockUseFeature = vi.mocked(useFeature);

describe('OperatorFeature', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders WaitlistShellLayout when full_operator_app is false', () => {
    mockUseFeature.mockReturnValue(false);

    render(<OperatorFeature />, { route: '/operator/waitlist' });

    expect(screen.getByTestId('waitlist-page')).toBeInTheDocument();
    expect(screen.queryByText('Tee Sheet')).not.toBeInTheDocument();
    expect(screen.queryByText('Settings')).not.toBeInTheDocument();
  });

  it('renders OperatorLayout when full_operator_app is true', () => {
    mockUseFeature.mockReturnValue(true);

    render(<OperatorFeature />, { route: '/operator/tee-sheet' });

    expect(screen.getByTestId('operator-layout')).toBeInTheDocument();
  });
});
