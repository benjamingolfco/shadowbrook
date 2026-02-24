import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@/test/test-utils';
import { DevRoleSwitcher } from '../components/DevRoleSwitcher';
import { useAuth } from '../hooks/useAuth';
import { useTenantContext } from '@/features/operator/context/TenantContext';

vi.mock('../hooks/useAuth');
vi.mock('@/features/operator/context/TenantContext');

const mockUseAuth = vi.mocked(useAuth);
const mockUseTenantContext = vi.mocked(useTenantContext);

const mockSetRole = vi.fn();
const mockClearTenant = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();

  mockUseAuth.mockReturnValue({
    user: null,
    role: 'operator',
    isAuthenticated: true,
    login: vi.fn(),
    logout: vi.fn(),
    setRole: mockSetRole,
  });

  mockUseTenantContext.mockReturnValue({
    tenant: { id: 'tenant-1', organizationName: 'Pine Valley Golf Club' },
    selectTenant: vi.fn(),
    clearTenant: mockClearTenant,
  });
});

describe('DevRoleSwitcher', () => {
  it('renders the Change Org button', () => {
    render(<DevRoleSwitcher />);

    expect(screen.getByRole('button', { name: 'Change Org' })).toBeInTheDocument();
  });

  it('calls clearTenant when Change Org button is clicked', () => {
    render(<DevRoleSwitcher />);

    act(() => {
      screen.getByRole('button', { name: 'Change Org' }).click();
    });

    expect(mockClearTenant).toHaveBeenCalledTimes(1);
  });

  it('renders the role dropdown with all three roles', () => {
    render(<DevRoleSwitcher />);

    const select = screen.getByRole('combobox');
    expect(select).toBeInTheDocument();

    expect(screen.getByRole('option', { name: 'Role: Admin' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Role: Operator' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Role: Golfer' })).toBeInTheDocument();
  });

  it('calls setRole with selected role when dropdown changes', () => {
    render(<DevRoleSwitcher />);

    const select = screen.getByRole('combobox');

    act(() => {
      Object.defineProperty(select, 'value', { value: 'golfer', configurable: true });
      select.dispatchEvent(new Event('change', { bubbles: true }));
    });

    expect(mockSetRole).toHaveBeenCalledWith('golfer');
  });
});
