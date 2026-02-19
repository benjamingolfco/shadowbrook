import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@/test/test-utils';
import { userEvent } from '@testing-library/user-event';
import CourseCreate from '../pages/CourseCreate';

vi.mock('../hooks/useTenants');

import { useTenants } from '../hooks/useTenants';

const mockUseTenants = vi.mocked(useTenants);
const mockNavigate = vi.fn();

vi.mock('react-router', async () => {
  const actual = await vi.importActual('react-router');
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe('CourseCreate', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state for tenant dropdown', () => {
    mockUseTenants.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<CourseCreate />);
    expect(screen.getByText('Loading tenants...')).toBeInTheDocument();
  });

  it('shows error message when tenants fail to load', () => {
    mockUseTenants.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useTenants>);

    render(<CourseCreate />);
    expect(screen.getByText(/Error loading tenants/)).toBeInTheDocument();
    expect(screen.getByText(/Network error/)).toBeInTheDocument();
  });

  it('shows empty state with link to create tenant', () => {
    mockUseTenants.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<CourseCreate />);
    expect(screen.getByText('No tenants available')).toBeInTheDocument();
    expect(screen.getByText(/No tenants found/)).toBeInTheDocument();

    const createTenantLink = screen.getByRole('link', { name: 'Create a tenant' });
    expect(createTenantLink).toHaveAttribute('href', '/admin/tenants/new');
  });

  it('renders tenant dropdown with sorted tenants', () => {
    mockUseTenants.mockReturnValue({
      data: [
        {
          id: '2',
          organizationName: 'Zenith Golf Club',
          contactName: 'Jane Doe',
          contactEmail: 'jane@zenith.com',
          contactPhone: '555-0102',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
        {
          id: '1',
          organizationName: 'Alpine Golf Course',
          contactName: 'John Doe',
          contactEmail: 'john@alpine.com',
          contactPhone: '555-0101',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<CourseCreate />);

    const tenantLabel = screen.getByText('Assign to Tenant *');
    expect(tenantLabel).toBeInTheDocument();
    expect(screen.getByText('Select a tenant')).toBeInTheDocument();
  });

  it('shows validation error when tenant is not selected', async () => {
    const user = userEvent.setup();

    mockUseTenants.mockReturnValue({
      data: [
        {
          id: '1',
          organizationName: 'Alpine Golf Course',
          contactName: 'John Doe',
          contactEmail: 'john@alpine.com',
          contactPhone: '555-0101',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    render(<CourseCreate />);

    const courseNameInput = screen.getByLabelText('Course Name *');
    await user.type(courseNameInput, 'Pine Valley');

    const submitButton = screen.getByRole('button', { name: 'Register Course' });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Tenant assignment is required')).toBeInTheDocument();
    });
  });

  it('has correct tab order: tenant before course name', () => {
    mockUseTenants.mockReturnValue({
      data: [
        {
          id: '1',
          organizationName: 'Alpine Golf Course',
          contactName: 'John Doe',
          contactEmail: 'john@alpine.com',
          contactPhone: '555-0101',
          createdAt: '2024-01-15T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useTenants>);

    const { container } = render(<CourseCreate />);

    const tenantTrigger = screen.getByRole('combobox', { name: 'Assign to Tenant *' });
    const courseNameInput = screen.getByLabelText('Course Name *');

    expect(tenantTrigger).toBeInTheDocument();
    expect(courseNameInput).toBeInTheDocument();

    // Verify tenant field comes before course name in DOM order
    const formElements = container.querySelectorAll('input, button[role="combobox"]');
    const tenantIndex = Array.from(formElements).indexOf(tenantTrigger);
    const courseNameIndex = Array.from(formElements).indexOf(courseNameInput);

    expect(tenantIndex).toBeLessThan(courseNameIndex);
  });
});
