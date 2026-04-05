import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

export interface SelectedCourse {
  id: string;
  name: string;
  timeZoneId: string;
}

interface CourseContextValue {
  course: SelectedCourse | null;
  selectCourse: (course: SelectedCourse) => void;
  clearCourse: () => void;
  isDirty: boolean;
  registerDirtyForm: (formId: string) => void;
  unregisterDirtyForm: (formId: string) => void;
}

const CourseContext = createContext<CourseContextValue | undefined>(undefined);

const STORAGE_KEY = 'teeforce-dev-course';

interface CourseProviderProps {
  children: ReactNode;
}

export function CourseProvider({ children }: CourseProviderProps) {
  const [course, setCourse] = useState<SelectedCourse | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as SelectedCourse;
    } catch {
      return null;
    }
  });

  const [dirtyForms, setDirtyForms] = useState<Set<string>>(new Set());

  const isDirty = dirtyForms.size > 0;

  const registerDirtyForm = useCallback((formId: string) => {
    setDirtyForms((prev) => new Set(prev).add(formId));
  }, []);

  const unregisterDirtyForm = useCallback((formId: string) => {
    setDirtyForms((prev) => {
      const next = new Set(prev);
      next.delete(formId);
      return next;
    });
  }, []);

  const selectCourse = (newCourse: SelectedCourse) => {
    setCourse(newCourse);
    setDirtyForms(new Set());
    localStorage.setItem(STORAGE_KEY, JSON.stringify(newCourse));
  };

  const clearCourse = () => {
    setCourse(null);
    setDirtyForms(new Set());
    localStorage.removeItem(STORAGE_KEY);
  };

  return (
    <CourseContext.Provider value={{ course, selectCourse, clearCourse, isDirty, registerDirtyForm, unregisterDirtyForm }}>
      {children}
    </CourseContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCourseContext() {
  const context = useContext(CourseContext);
  if (context === undefined) {
    throw new Error('useCourseContext must be used within a CourseProvider');
  }
  return context;
}
