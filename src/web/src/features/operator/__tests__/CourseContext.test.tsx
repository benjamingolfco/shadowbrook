import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@/test/test-utils';
import { CourseProvider, useCourseContext } from '../context/CourseContext';

// Consumer component that exposes context values for testing
function TestConsumer() {
  const { course, selectCourse, clearCourse, isDirty, registerDirtyForm, unregisterDirtyForm } = useCourseContext();

  return (
    <div>
      <span data-testid="course-id">{course?.id ?? 'null'}</span>
      <span data-testid="course-name">{course?.name ?? 'null'}</span>
      <span data-testid="is-dirty">{String(isDirty)}</span>
      <button
        onClick={() => selectCourse({ id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/Chicago' })}
        data-testid="select-btn"
      >
        Select Course
      </button>
      <button onClick={() => clearCourse()} data-testid="clear-btn">
        Clear Course
      </button>
      <button onClick={() => registerDirtyForm('form-1')} data-testid="register-dirty-btn">
        Register Dirty
      </button>
      <button onClick={() => unregisterDirtyForm('form-1')} data-testid="unregister-dirty-btn">
        Unregister Dirty
      </button>
      <button onClick={() => registerDirtyForm('form-2')} data-testid="register-dirty-2-btn">
        Register Dirty 2
      </button>
      <button onClick={() => unregisterDirtyForm('form-2')} data-testid="unregister-dirty-2-btn">
        Unregister Dirty 2
      </button>
    </div>
  );
}

function renderWithProvider() {
  return render(
    <CourseProvider>
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
    const stored = { id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/Chicago' };
    localStorage.setItem('teeforce-dev-course', JSON.stringify(stored));

    renderWithProvider();

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

    const stored = JSON.parse(localStorage.getItem('teeforce-dev-course') ?? 'null') as {
      id: string; name: string; timeZoneId: string;
    };
    expect(stored).toEqual({ id: 'course-1', name: 'Pine Valley', timeZoneId: 'America/Chicago' });
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
    expect(localStorage.getItem('teeforce-dev-course')).toBeNull();
  });

  it('throws error when used outside CourseProvider', () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => {
      render(<TestConsumer />);
    }).toThrow('useCourseContext must be used within a CourseProvider');

    spy.mockRestore();
  });

  it('handles corrupt localStorage gracefully', () => {
    localStorage.setItem('teeforce-dev-course', 'not-valid-json{{{');

    // Should not throw, should return null course
    renderWithProvider();

    expect(screen.getByTestId('course-id').textContent).toBe('null');
  });

  // Dirty form tracking tests
  it('isDirty is false when no forms are registered', () => {
    renderWithProvider();

    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
  });

  it('registerDirtyForm makes isDirty true', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('register-dirty-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('true');
  });

  it('unregisterDirtyForm makes isDirty false when last form unregistered', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('register-dirty-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('true');

    act(() => {
      screen.getByTestId('unregister-dirty-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
  });

  it('multiple dirty forms tracked independently', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('register-dirty-btn').click();
    });
    act(() => {
      screen.getByTestId('register-dirty-2-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('true');

    act(() => {
      screen.getByTestId('unregister-dirty-btn').click();
    });

    // Still dirty because form-2 is still registered
    expect(screen.getByTestId('is-dirty').textContent).toBe('true');

    act(() => {
      screen.getByTestId('unregister-dirty-2-btn').click();
    });

    // Now both are unregistered
    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
  });

  it('selectCourse clears dirty forms', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('register-dirty-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('true');

    act(() => {
      screen.getByTestId('select-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
  });

  it('clearCourse clears dirty forms', () => {
    renderWithProvider();

    act(() => {
      screen.getByTestId('register-dirty-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('true');

    act(() => {
      screen.getByTestId('clear-btn').click();
    });

    expect(screen.getByTestId('is-dirty').textContent).toBe('false');
  });
});
