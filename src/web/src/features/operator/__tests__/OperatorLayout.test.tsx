import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorLayout from '@/components/layout/OperatorLayout';
import { useTenantContext } from '../context/TenantContext';
import { useCourseContext } from '../context/CourseContext';

vi.mock('../context/TenantContext');
vi.mock('../context/CourseContext');

const mockUseTenantContext = vi.mocked(useTenantContext);
const mockUseCourseContext = vi.mocked(useCourseContext);

const mockClearCourse = vi.fn();

describe('OperatorLayout', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: tenant selected, course selected
    mockUseTenantContext.mockReturnValue({
      tenant: { id: '1', organizationName: 'Pine Valley Golf Club' },
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });
    mockUseCourseContext.mockReturnValue({
      course: { id: '1', name: 'Test Course' },
      selectCourse: vi.fn(),
      clearCourse: mockClearCourse,
    });
  });

  it('shows selected organization name in sidebar header', () => {
    render(<OperatorLayout />);
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
  });

  it('shows Shadowbrook when no tenant is selected', () => {
    mockUseTenantContext.mockReturnValue({
      tenant: null,
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });

    render(<OperatorLayout />);
    expect(screen.getByText('Shadowbrook')).toBeInTheDocument();
  });

  it('applies truncate class and title attribute for long organization names', () => {
    const longName = 'Very Long Organization Name That Should Be Truncated';
    mockUseTenantContext.mockReturnValue({
      tenant: { id: '1', organizationName: longName },
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });

    render(<OperatorLayout />);
    const heading = screen.getByText(longName);
    expect(heading).toHaveClass('truncate');
    expect(heading).toHaveClass('max-w-[200px]');
    expect(heading).toHaveAttribute('title', longName);
  });

  it('shows Change Organization button when tenant is selected', () => {
    render(<OperatorLayout />);
    expect(screen.getByRole('button', { name: 'Change Organization' })).toBeInTheDocument();
  });

  it('does not show Change Organization button when no tenant is selected', () => {
    mockUseTenantContext.mockReturnValue({
      tenant: null,
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });

    render(<OperatorLayout />);
    expect(screen.queryByRole('button', { name: 'Change Organization' })).not.toBeInTheDocument();
  });

  it('calls clearTenant when Change Organization button is clicked', () => {
    const mockClearTenant = vi.fn();
    mockUseTenantContext.mockReturnValue({
      tenant: { id: '1', organizationName: 'Pine Valley Golf Club' },
      selectTenant: vi.fn(),
      clearTenant: mockClearTenant,
    });

    render(<OperatorLayout />);
    const button = screen.getByRole('button', { name: 'Change Organization' });
    button.click();

    expect(mockClearTenant).toHaveBeenCalled();
  });

  it('shows course name in sidebar when course is selected', () => {
    mockUseCourseContext.mockReturnValue({
      course: { id: '1', name: 'Spyglass Hill' },
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
    });

    render(<OperatorLayout />);
    expect(screen.getByText('Spyglass Hill')).toBeInTheDocument();
  });

  it('does not show course name when no course is selected', () => {
    mockUseCourseContext.mockReturnValue({
      course: null,
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
    });

    render(<OperatorLayout />);
    // Course name paragraph should not be present
    expect(screen.queryByTitle('Spyglass Hill')).not.toBeInTheDocument();
  });

  it('shows Change Course button when course is selected', () => {
    mockUseCourseContext.mockReturnValue({
      course: { id: '1', name: 'Spyglass Hill' },
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
    });

    render(<OperatorLayout />);
    expect(screen.getByRole('button', { name: 'Change Course' })).toBeInTheDocument();
  });

  it('does not show Change Course button when no course is selected', () => {
    mockUseCourseContext.mockReturnValue({
      course: null,
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
    });

    render(<OperatorLayout />);
    expect(screen.queryByRole('button', { name: 'Change Course' })).not.toBeInTheDocument();
  });

  it('calls clearCourse when Change Course button is clicked', () => {
    mockUseCourseContext.mockReturnValue({
      course: { id: '1', name: 'Spyglass Hill' },
      selectCourse: vi.fn(),
      clearCourse: mockClearCourse,
    });

    render(<OperatorLayout />);
    const button = screen.getByRole('button', { name: 'Change Course' });
    button.click();

    expect(mockClearCourse).toHaveBeenCalled();
  });

  it('course name truncates with title attribute for long names', () => {
    const longCourseName = 'Augusta National Golf Club Championship Course';
    mockUseCourseContext.mockReturnValue({
      course: { id: '1', name: longCourseName },
      selectCourse: vi.fn(),
      clearCourse: vi.fn(),
    });

    render(<OperatorLayout />);
    const courseNameEl = screen.getByText(longCourseName);
    expect(courseNameEl).toHaveClass('truncate');
    expect(courseNameEl).toHaveClass('max-w-[200px]');
    expect(courseNameEl).toHaveAttribute('title', longCourseName);
  });
});
