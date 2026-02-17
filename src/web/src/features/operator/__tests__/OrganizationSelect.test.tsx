import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OrganizationSelect from '../pages/OrganizationSelect';
import { useTenants } from '@/hooks/useTenants';
import { useTenantContext } from '../context/TenantContext';

vi.mock('@/hooks/useTenants');
vi.mock('../context/TenantContext');

const mockUseTenants = vi.mocked(useTenants);
const mockUseTenantContext = vi.mocked(useTenantContext);

describe('OrganizationSelect', () => {
  beforeEach(() => {
    mockUseTenantContext.mockReturnValue({
      tenant: null,
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });
  });

  it('shows loading state with skeleton rows', () => {
    mockUseTenants.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<OrganizationSelect />);
    expect(screen.getByText('Select Organization')).toBeInTheDocument();
    expect(screen.getByRole('table')).toBeInTheDocument();
  });

  it('shows error state', () => {
    mockUseTenants.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useTenants>);

    render(<OrganizationSelect />);
    expect(screen.getByText(/Error loading organizations/)).toBeInTheDocument();
    expect(screen.getByText(/Network error/)).toBeInTheDocument();
  });

  it('shows empty state message', () => {
    mockUseTenants.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<OrganizationSelect />);
    expect(screen.getByText('Select Organization')).toBeInTheDocument();
    expect(
      screen.getByText(/No organizations registered yet/),
    ).toBeInTheDocument();
    expect(screen.getByText(/register a tenant via the Admin view/)).toBeInTheDocument();
  });

  it('renders tenant list with organization names', () => {
    mockUseTenants.mockReturnValue({
      data: [
        {
          id: '1',
          organizationName: 'Pine Valley Golf Club',
          contactName: 'John Doe',
          contactEmail: 'john@pinevalley.com',
          contactPhone: '555-1234',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
        {
          id: '2',
          organizationName: 'Augusta National',
          contactName: 'Jane Smith',
          contactEmail: 'jane@augusta.com',
          contactPhone: '555-5678',
          createdAt: '2024-01-16T00:00:00Z',
          updatedAt: '2024-01-16T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<OrganizationSelect />);
    expect(screen.getByText('Pine Valley Golf Club')).toBeInTheDocument();
    expect(screen.getByText('Augusta National')).toBeInTheDocument();
  });

  it('calls selectTenant when clicking a tenant row', () => {
    const mockSelectTenant = vi.fn();
    mockUseTenantContext.mockReturnValue({
      tenant: null,
      selectTenant: mockSelectTenant,
      clearTenant: vi.fn(),
    });

    mockUseTenants.mockReturnValue({
      data: [
        {
          id: '1',
          organizationName: 'Pine Valley Golf Club',
          contactName: 'John Doe',
          contactEmail: 'john@pinevalley.com',
          contactPhone: '555-1234',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<OrganizationSelect />);
    const row = screen.getByText('Pine Valley Golf Club').closest('tr');
    row?.click();

    expect(mockSelectTenant).toHaveBeenCalledWith({
      id: '1',
      organizationName: 'Pine Valley Golf Club',
    });
  });
});
