import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorFeature from '../index';

vi.mock('@/hooks/use-features', () => ({
  useFeature: vi.fn(),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: vi.fn(() => ({
    user: { displayName: 'Test User', email: 'test@test.com', organization: { name: 'Test Org' }, role: 'Operator', courses: [{ id: 'course-1', name: 'Test Course' }] },
    logout: vi.fn(),
    courses: [{ id: 'course-1', name: 'Test Course' }],
    organizations: [],
  })),
}));

vi.mock('@/features/auth', () => ({
  useAuth: vi.fn(() => ({
    user: { displayName: 'Test User', email: 'test@test.com', organization: { name: 'Test Org' }, role: 'Operator', courses: [{ id: 'course-1', name: 'Test Course' }] },
    logout: vi.fn(),
    courses: [{ id: 'course-1', name: 'Test Course' }],
    organizations: [],
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

vi.mock('../context/OrgContext', () => ({
  OrgProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useOrgContext: vi.fn(() => ({
    org: null,
    selectOrg: vi.fn(),
    clearOrg: vi.fn(),
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

vi.mock('../pages/TeeTimeSettings', () => ({
  default: () => <div data-testid="settings-page">Settings</div>,
}));

vi.mock('../pages/CoursePortfolio', () => ({
  default: () => <div data-testid="course-portfolio-page">Course Portfolio</div>,
}));

// Mock AppShell (heavy: shadcn Sidebar provider) with a pass-through that exposes
// its variant via data-testid so the routing tests can assert which variant
// the operator feature picked.
vi.mock('@/components/layout/AppShell', () => ({
  AppShell: ({ variant, children }: { variant: 'full' | 'minimal'; children: React.ReactNode }) => (
    <div data-testid={`app-shell-${variant}`}>{children}</div>
  ),
}));

import { useFeature } from '@/hooks/use-features';

const mockUseFeature = vi.mocked(useFeature);

describe('OperatorFeature', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mounts the minimal AppShell variant when full_operator_app is false', () => {
    mockUseFeature.mockReturnValue(false);

    render(<OperatorFeature />, { route: '/waitlist' });

    expect(screen.getByTestId('app-shell-minimal')).toBeInTheDocument();
    expect(screen.getByTestId('waitlist-page')).toBeInTheDocument();
    expect(screen.queryByTestId('tee-sheet-page')).not.toBeInTheDocument();
    expect(screen.queryByTestId('settings-page')).not.toBeInTheDocument();
  });

  it('mounts the full AppShell variant when full_operator_app is true', () => {
    mockUseFeature.mockReturnValue(true);

    render(<OperatorFeature />, { route: '/tee-sheet' });

    expect(screen.getByTestId('app-shell-full')).toBeInTheDocument();
    expect(screen.getByTestId('tee-sheet-page')).toBeInTheDocument();
  });
});
