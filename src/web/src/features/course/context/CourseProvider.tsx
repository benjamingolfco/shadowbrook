import { createContext, useContext, type ReactNode } from 'react';
import { useCourseId } from '../hooks/useCourseId';
import { useCourse } from '../hooks/useCourse';

interface CourseContextValue {
  course: { id: string; name: string; timeZoneId: string } | null;
  isLoading: boolean;
}

const CourseContext = createContext<CourseContextValue | undefined>(undefined);

interface CourseProviderProps {
  children: ReactNode;
}

export function CourseProvider({ children }: CourseProviderProps) {
  const courseId = useCourseId();
  const { data, isLoading } = useCourse(courseId);

  const course = data
    ? { id: data.id, name: data.name, timeZoneId: data.timeZoneId }
    : null;

  return (
    <CourseContext.Provider value={{ course, isLoading }}>
      {children}
    </CourseContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useCourseContext(): CourseContextValue {
  const context = useContext(CourseContext);
  if (context === undefined) {
    throw new Error('useCourseContext must be used within a CourseProvider');
  }
  return context;
}
