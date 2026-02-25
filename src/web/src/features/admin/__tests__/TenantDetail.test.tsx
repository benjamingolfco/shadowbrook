import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@/test/test-utils';
import TenantDetail from '../pages/TenantDetail';

vi.mock('../hooks/useTenants');
vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useParams: () => ({ id: 'test-tenant-id' }),
  };
});

import { useTenant } from '../hooks/useTenants';

const mockUseTenant = vi.mocked(useTenant);

const sampleTenant = {
  id: 'test-tenant-id',
  organizationName: 'Pinehurst Golf Club',
  contactName: 'Alice Brown',
  contactEmail: 'alice@pinehurst.com',
  contactPhone: '555-9999',
  courseCount: 2,
  createdAt: '2024-06-01T00:00:00Z',
  updatedAt: '2024-06-01T00:00:00Z',
  courses: [
    {
      id: 'course-1',
      name: 'Course No. 2',
      city: 'Pinehurst',
      state: 'NC',
    },
    {
      id: 'course-2',
      name: 'Course No. 4',
      city: null,
      state: null,
    },
  ],
};

describe('TenantDetail', () => {
  it('shows loading state with skeletons', () => {
    mockUseTenant.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useTenant>);

    const { container } = render(<TenantDetail />);
    expect(container.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThan(0);
  });

  it('shows not-found state for 404 error', () => {
    const notFoundError = Object.assign(new Error('Not found'), { status: 404 });
    mockUseTenant.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: notFoundError,
    } as unknown as ReturnType<typeof useTenant>);

    render(<TenantDetail />);
    expect(screen.getByText('Tenant not found')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to Tenants' })).toHaveAttribute(
      'href',
      '/admin/tenants',
    );
  });

  it('shows generic error state', () => {
    mockUseTenant.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Server error'),
    } as unknown as ReturnType<typeof useTenant>);

    render(<TenantDetail />);
    expect(screen.getByText('Server error')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to Tenants' })).toHaveAttribute(
      'href',
      '/admin/tenants',
    );
  });

  it('renders tenant details', () => {
    mockUseTenant.mockReturnValue({
      data: sampleTenant,
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenant>);

    render(<TenantDetail />);
    expect(screen.getByRole('heading', { level: 1, name: 'Pinehurst Golf Club' })).toBeInTheDocument();
    expect(screen.getByText('Alice Brown')).toBeInTheDocument();
    expect(screen.getByText('alice@pinehurst.com')).toBeInTheDocument();
    expect(screen.getByText('555-9999')).toBeInTheDocument();
    expect(screen.getByText(/Registered/)).toBeInTheDocument();
  });

  it('renders courses table with location', () => {
    mockUseTenant.mockReturnValue({
      data: sampleTenant,
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenant>);

    render(<TenantDetail />);
    expect(screen.getByText('Course No. 2')).toBeInTheDocument();
    expect(screen.getByText('Pinehurst, NC')).toBeInTheDocument();
  });

  it('shows empty state when tenant has no courses', () => {
    mockUseTenant.mockReturnValue({
      data: { ...sampleTenant, courses: [], courseCount: 0 },
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenant>);

    render(<TenantDetail />);
    expect(screen.getByText('No courses assigned to this tenant yet.')).toBeInTheDocument();
  });

  it('shows em dash for courses without location', () => {
    mockUseTenant.mockReturnValue({
      data: sampleTenant,
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenant>);

    const { container } = render(<TenantDetail />);
    // Course No. 4 has no city or state — should render an em dash
    expect(container.textContent).toContain('—');
  });

  it('back link navigates to tenant list', () => {
    mockUseTenant.mockReturnValue({
      data: sampleTenant,
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenant>);

    render(<TenantDetail />);
    const backLink = screen.getByRole('link', { name: /Back to Tenants/ });
    expect(backLink).toHaveAttribute('href', '/admin/tenants');
  });
});
