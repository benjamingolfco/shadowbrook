import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Routes, Route } from 'react-router';
import { type ReactNode } from 'react';
import CourseFeature from '../index';
import { useFeatures } from '@/hooks/use-features';

// Mock all heavy layouts and pages so we can test routing without full render trees
vi.mock('../layouts/PickerLayout', () => ({
  default: ({ children }: { children: ReactNode }) => <div>{children}</div>,
}));
vi.mock('../manage/layouts/ManagementLayout', () => ({
  default: () => <div data-testid="management-layout">Management</div>,
}));
vi.mock('../pos/layouts/PosLayout', () => ({
  default: () => <div data-testid="pos-layout">POS</div>,
}));
vi.mock('../pages/CoursePicker', () => ({
  default: () => <div data-testid="course-picker">Picker</div>,
}));
vi.mock('../context/CourseProvider', () => ({
  CourseProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));
vi.mock('../context/OrgContext', () => ({
  OrgProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));
vi.mock('@/components/ThemeProvider', () => ({
  ThemeProvider: ({ children }: { children: ReactNode }) => <>{children}</>,
}));
vi.mock('@/components/SplashScreen', () => ({
  default: () => <div data-testid="splash-screen">Loading</div>,
}));
vi.mock('@/hooks/use-features');

const mockUseFeatures = vi.mocked(useFeatures);

function renderAtPath(path: string) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/course/*" element={<CourseFeature />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('CourseFeature routing', () => {
  it('renders SplashScreen while feature flags are loading', () => {
    mockUseFeatures.mockReturnValue({
      data: undefined,
      isLoading: true,
    } as unknown as ReturnType<typeof useFeatures>);

    renderAtPath('/course/course-123/manage');

    expect(screen.getByTestId('splash-screen')).toBeInTheDocument();
    expect(screen.queryByTestId('management-layout')).not.toBeInTheDocument();
    expect(screen.queryByTestId('pos-layout')).not.toBeInTheDocument();
  });

  it('renders management layout when full-operator-app flag is true', () => {
    mockUseFeatures.mockReturnValue({
      data: { 'full-operator-app': true },
      isLoading: false,
    } as unknown as ReturnType<typeof useFeatures>);

    renderAtPath('/course/course-123/manage');

    expect(screen.getByTestId('management-layout')).toBeInTheDocument();
    expect(screen.queryByTestId('splash-screen')).not.toBeInTheDocument();
  });

  it('renders POS layout when full-operator-app flag is false', () => {
    mockUseFeatures.mockReturnValue({
      data: { 'full-operator-app': false },
      isLoading: false,
    } as unknown as ReturnType<typeof useFeatures>);

    renderAtPath('/course/course-123/pos/waitlist');

    expect(screen.getByTestId('pos-layout')).toBeInTheDocument();
    expect(screen.queryByTestId('management-layout')).not.toBeInTheDocument();
    expect(screen.queryByTestId('splash-screen')).not.toBeInTheDocument();
  });

  it('does not render management routes when full-operator-app flag is false', () => {
    mockUseFeatures.mockReturnValue({
      data: { 'full-operator-app': false },
      isLoading: false,
    } as unknown as ReturnType<typeof useFeatures>);

    renderAtPath('/course/course-123/manage');

    expect(screen.queryByTestId('management-layout')).not.toBeInTheDocument();
  });
});
