import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@/test/test-utils';
import OperatorLayout from '@/components/layout/OperatorLayout';
import { useTenantContext } from '../context/TenantContext';

vi.mock('../context/TenantContext');

const mockUseTenantContext = vi.mocked(useTenantContext);

describe('OperatorLayout', () => {
  it('shows selected organization name in sidebar header', () => {
    mockUseTenantContext.mockReturnValue({
      tenant: { id: '1', organizationName: 'Pine Valley Golf Club' },
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });

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
    mockUseTenantContext.mockReturnValue({
      tenant: { id: '1', organizationName: 'Pine Valley Golf Club' },
      selectTenant: vi.fn(),
      clearTenant: vi.fn(),
    });

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
});
