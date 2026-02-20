import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@/test/test-utils';
import { CourseProvider, useCourseContext } from '../context/CourseContext';

// Consumer component that exposes context values for testing
function TestConsumer() {
  const { course, selectCourse, clearCourse } = useCourseContext();

  return (
    <div>
      <span data-testid="course-id">{course?.id ?? 'null'}</span>
      <span data-testid="course-name">{course?.name ?? 'null'}</span>
      <button
        onClick={() => selectCourse({ id: 'course-1', name: 'Pine Valley' })}
        data-testid="select-btn"
      >
        Select Course
      </button>
      <button onClick={() => clearCourse()} data-testid="clear-btn">
        Clear Course
      </button>
    </div>
  );
}

function renderWithProvider(tenantId = 'tenant-1') {
  return render(
    <CourseProvider tenantId={tenantId}>
      <TestConsumer />
    </CourseProvider>,
  );
}

describe('CourseContext', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('initial state is null when localStorage is empty', () => {
    renderWithProvider();

    expect(screen.getByTestId('course-id').textContent).toBe('null');
    expect(screen.getByTestId('course-name').textContent).toBe('null');
  });

  it('restores course from localStorage on mount', () => {
    const stored = {
      course: { id: 'course-1', name: 'Pine Valley' },
      tenantId: 'tenant-1',
    };
    localStorage.setItem('shadowbrook-dev-course', JSON.stringify(stored));

    renderWithProvider('tenant-1');

    expect(screen.getByTestId('course-id').textContent).toBe('course-1');
    expect(screen.getByTestId('course-name').textContent).toBe('Pine Valley');
  });

  it('selectCourse updates state and localStorage', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('select-btn').click();
    });

    expect(screen.getByTestId('course-id').textContent).toBe('course-1');
    expect(screen.getByTestId('course-name').textContent).toBe('Pine Valley');

    const stored = JSON.parse(localStorage.getItem('shadowbrook-dev-course') ?? 'null') as {
      course: { id: string; name: string };
      tenantId: string;
    };
    expect(stored.course).toEqual({ id: 'course-1', name: 'Pine Valley' });
  });

  it('clearCourse resets state and removes localStorage', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('select-btn').click();
    });

    expect(screen.getByTestId('course-id').textContent).toBe('course-1');

    act(() => {
      screen.getByTestId('clear-btn').click();
    });

    expect(screen.getByTestId('course-id').textContent).toBe('null');
    expect(localStorage.getItem('shadowbrook-dev-course')).toBeNull();
  });

  it('clears course when tenantId changes (remount via key)', () => {
    // Store a course for tenant-1
    const stored = {
      course: { id: 'course-1', name: 'Pine Valley' },
      tenantId: 'tenant-1',
    };
    localStorage.setItem('shadowbrook-dev-course', JSON.stringify(stored));

    // Render with tenant-2 — stored tenantId (tenant-1) won't match
    renderWithProvider('tenant-2');

    // Course should be null because tenantId mismatch on init
    expect(screen.getByTestId('course-id').textContent).toBe('null');
    expect(localStorage.getItem('shadowbrook-dev-course')).toBeNull();
  });

  it('throws error when used outside CourseProvider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => {
      render(<TestConsumer />);
    }).toThrow('useCourseContext must be used within a CourseProvider');

    spy.mockRestore();
  });

  it('handles corrupt localStorage gracefully', () => {
    localStorage.setItem('shadowbrook-dev-course', 'not-valid-json{{{');

    // Should not throw, should return null course
    renderWithProvider();

    expect(screen.getByTestId('course-id').textContent).toBe('null');
  });

  it('does not restore course when tenantId in storage mismatches current tenant', () => {
    const stored = {
      course: { id: 'course-1', name: 'Pine Valley' },
      tenantId: 'tenant-1',
    };
    localStorage.setItem('shadowbrook-dev-course', JSON.stringify(stored));

    // Render with a different tenant — course should not restore
    renderWithProvider('tenant-99');

    expect(screen.getByTestId('course-id').textContent).toBe('null');
  });
});
