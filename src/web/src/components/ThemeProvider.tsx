import { createContext, useContext, useEffect, type ReactNode } from 'react';
import { applyTheme, clearTheme, type CourseTheme } from '@/lib/theme';

interface ThemeContextValue {
  /** The active course theme overrides, if any. */
  courseTheme?: CourseTheme;
}

const ThemeContext = createContext<ThemeContextValue>({});

// eslint-disable-next-line react-refresh/only-export-components
export function useTheme() {
  return useContext(ThemeContext);
}

interface ThemeProviderProps {
  children: ReactNode;
  /** Optional per-course theme overrides. */
  courseTheme?: CourseTheme;
}

export function ThemeProvider({ children, courseTheme }: ThemeProviderProps) {
  useEffect(() => {
    applyTheme(document.documentElement, courseTheme);
    return () => {
      clearTheme(document.documentElement);
    };
  }, [courseTheme]);

  return (
    <ThemeContext.Provider value={{ courseTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}
