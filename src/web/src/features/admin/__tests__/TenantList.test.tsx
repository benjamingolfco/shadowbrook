import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@/test/test-utils';
import TenantList from '../pages/TenantList';

vi.mock('../hooks/useTenants');
vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

import { useTenants } from '../hooks/useTenants';

const mockUseTenants = vi.mocked(useTenants);
const mockNavigate = vi.fn();

const sampleTenants = [
  {
    id: 'tenant-1',
    organizationName: 'Pine Valley Golf Club',
    contactName: 'John Smith',
    contactEmail: 'john@pinevalley.com',
    contactPhone: '555-0001',
    courseCount: 2,
    createdAt: '2024-01-15T00:00:00Z',
    updatedAt: '2024-01-15T00:00:00Z',
  },
  {
    id: 'tenant-2',
    organizationName: 'Desert Dunes',
    contactName: 'Jane Doe',
    contactEmail: 'jane@desertdunes.com',
    contactPhone: '555-0002',
    courseCount: 0,
    createdAt: '2024-02-20T00:00:00Z',
    updatedAt: '2024-02-20T00:00:00Z',
  },
  {
    id: 'tenant-3',
    organizationName: 'Mountain Links',
    contactName: 'Bob Wilson',
    contactEmail: 'bob@mountainlinks.com',
    contactPhone: '555-0003',
    courseCount: 5,
    createdAt: '2024-03-10T00:00:00Z',
    updatedAt: '2024-03-10T00:00:00Z',
  },
];

describe('TenantList', () => {
  it('shows loading state with skeletons', () => {
    mockUseTenants.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    const { container } = render(<TenantList />);
    expect(container.querySelectorAll('[data-slot="skeleton"]').length).toBeGreaterThan(0);
  });

  it('shows error state with try again button', () => {
    mockUseTenants.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    expect(screen.getByText('Network error')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument();
  });

  it('shows empty state when no tenants exist', () => {
    mockUseTenants.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    expect(screen.getByText('No tenants registered yet.')).toBeInTheDocument();
  });

  it('renders metric cards with correct counts', () => {
    mockUseTenants.mockReturnValue({
      data: sampleTenants,
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);

    // Total Tenants: 3
    const totalTenantsCard = screen.getByRole('generic', { name: 'Total tenants' });
    expect(totalTenantsCard).toHaveTextContent('3');

    // Total Courses: 2 + 0 + 5 = 7
    const totalCoursesCard = screen.getByRole('generic', { name: 'Total courses' });
    expect(totalCoursesCard).toHaveTextContent('7');

    // Tenants Without Courses: 1 (Desert Dunes)
    const noCoursesCard = screen.getByRole('generic', { name: 'Tenants without courses' });
    expect(noCoursesCard).toHaveTextContent('1');
  });

  it('renders tenant data in table', () => {
    mockUseTenants.mockReturnValue({
      data: [sampleTenants[0]],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.getByText('John Smith')).toBeInTheDocument();
    expect(screen.getByText('john@pinevalley.com')).toBeInTheDocument();
  });

  it('shows "No courses" badge for zero-course tenants', () => {
    mockUseTenants.mockReturnValue({
      data: [sampleTenants[1]],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    expect(screen.getByText('No courses')).toBeInTheDocument();
  });

  it('organization name links to tenant detail', () => {
    mockUseTenants.mockReturnValue({
      data: [sampleTenants[0]],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    const link = screen.getByRole('link', { name: 'Pine Valley Golf Club' });
    expect(link).toHaveAttribute('href', '/admin/tenants/tenant-1');
  });

  it('clickable row navigates to tenant detail', () => {
    mockNavigate.mockClear();
    mockUseTenants.mockReturnValue({
      data: [sampleTenants[0]],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    const row = screen.getByRole('link', { name: 'Pine Valley Golf Club' }).closest('tr')!;
    fireEvent.click(row);
    expect(mockNavigate).toHaveBeenCalledWith('/admin/tenants/tenant-1');
  });

  it('shows register tenant link', () => {
    mockUseTenants.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useTenants>);

    render(<TenantList />);
    expect(screen.getByRole('link', { name: 'Register Tenant' })).toHaveAttribute(
      'href',
      '/admin/tenants/new',
    );
  });
});
