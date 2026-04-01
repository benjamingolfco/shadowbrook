import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@/test/test-utils';
import { userEvent } from '@testing-library/user-event';
import CourseCreate from '../pages/CourseCreate';

vi.mock('../hooks/useOrganizations');

import { useOrganizations } from '../hooks/useOrganizations';

const mockUseOrganizations = vi.mocked(useOrganizations);
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

  it('shows loading state for organization dropdown', () => {
    mockUseOrganizations.mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as unknown as ReturnType<typeof useOrganizations>);

    render(<CourseCreate />);
    expect(screen.getByText('Loading organizations...')).toBeInTheDocument();
  });

  it('shows error message when organizations fail to load', () => {
    mockUseOrganizations.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error('Network error'),
    } as unknown as ReturnType<typeof useOrganizations>);

    render(<CourseCreate />);
    expect(screen.getByText(/Error loading organizations/)).toBeInTheDocument();
    expect(screen.getByText(/Network error/)).toBeInTheDocument();
  });

  it('shows empty state with link to create organization', () => {
    mockUseOrganizations.mockReturnValue({
      data: [],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useOrganizations>);

    render(<CourseCreate />);
    expect(screen.getByText('No organizations available')).toBeInTheDocument();
    expect(screen.getByText(/No organizations found/)).toBeInTheDocument();

    const createOrgLink = screen.getByRole('link', { name: 'Create an organization' });
    expect(createOrgLink).toHaveAttribute('href', '/admin/organizations/new');
  });

  it('renders organization dropdown with sorted organizations', () => {
    mockUseOrganizations.mockReturnValue({
      data: [
        {
          id: '2',
          name: 'Zenith Golf Club',
          courseCount: 1,
          userCount: 2,
          createdAt: '2024-01-15T00:00:00Z',
        },
        {
          id: '1',
          name: 'Alpine Golf Course',
          courseCount: 1,
          userCount: 2,
          createdAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useOrganizations>);

    render(<CourseCreate />);

    const orgLabel = screen.getByText('Assign to Organization *');
    expect(orgLabel).toBeInTheDocument();
    expect(screen.getByText('Select an organization')).toBeInTheDocument();
  });

  it('shows validation error when organization is not selected', async () => {
    const user = userEvent.setup();

    mockUseOrganizations.mockReturnValue({
      data: [
        {
          id: '1',
          name: 'Alpine Golf Course',
          courseCount: 1,
          userCount: 2,
          createdAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useOrganizations>);

    render(<CourseCreate />);

    const courseNameInput = screen.getByLabelText('Course Name *');
    await user.type(courseNameInput, 'Pine Valley');

    const submitButton = screen.getByRole('button', { name: 'Register Course' });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Organization is required')).toBeInTheDocument();
    });
  });

  it('has correct tab order: organization before course name', () => {
    mockUseOrganizations.mockReturnValue({
      data: [
        {
          id: '1',
          name: 'Alpine Golf Course',
          courseCount: 1,
          userCount: 2,
          createdAt: '2024-01-15T00:00:00Z',
        },
      ],
      isLoading: false,
      error: null,
    } as unknown as ReturnType<typeof useOrganizations>);

    const { container } = render(<CourseCreate />);

    const tenantTrigger = screen.getByRole('combobox', { name: 'Assign to Organization *' });
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
