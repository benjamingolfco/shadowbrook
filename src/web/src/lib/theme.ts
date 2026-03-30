/**
 * Theme color tokens that map to CSS custom properties.
 * Each value is an oklch() string (e.g., "oklch(0.35 0.06 155)").
 * Partial themes override only the specified tokens; unspecified tokens
 * fall back to the default theme.
 */
export interface ThemeColors {
  background: string;
  foreground: string;
  card: string;
  'card-foreground': string;
  popover: string;
  'popover-foreground': string;
  primary: string;
  'primary-foreground': string;
  secondary: string;
  'secondary-foreground': string;
  muted: string;
  'muted-foreground': string;
  accent: string;
  'accent-foreground': string;
  destructive: string;
  success: string;
  'success-foreground': string;
  border: string;
  input: string;
  ring: string;
  sidebar: string;
  'sidebar-foreground': string;
  'sidebar-primary': string;
  'sidebar-primary-foreground': string;
  'sidebar-accent': string;
  'sidebar-accent-foreground': string;
  'sidebar-border': string;
}

/**
 * A course theme override. Only specified keys are overridden;
 * everything else falls back to the default theme.
 */
export type CourseTheme = Partial<ThemeColors>;

/** The default "warm professional" theme. */
export const defaultTheme: ThemeColors = {
  // Warm linen off-white
  background: 'oklch(0.98 0.005 85)',
  foreground: 'oklch(0.22 0.01 60)',
  // Pure white cards for contrast against warm background
  card: 'oklch(1 0 0)',
  'card-foreground': 'oklch(0.22 0.01 60)',
  popover: 'oklch(1 0 0)',
  'popover-foreground': 'oklch(0.22 0.01 60)',
  // Deep forest green
  primary: 'oklch(0.35 0.06 155)',
  'primary-foreground': 'oklch(0.98 0.005 85)',
  // Warm stone
  secondary: 'oklch(0.95 0.008 80)',
  'secondary-foreground': 'oklch(0.30 0.01 60)',
  muted: 'oklch(0.94 0.008 80)',
  'muted-foreground': 'oklch(0.55 0.01 60)',
  // Warm amber accent
  accent: 'oklch(0.75 0.12 75)',
  'accent-foreground': 'oklch(0.25 0.02 60)',
  // Warm terracotta
  destructive: 'oklch(0.55 0.2 25)',
  // Natural sage green
  success: 'oklch(0.65 0.1 150)',
  'success-foreground': 'oklch(0.98 0.005 85)',
  // Warm borders
  border: 'oklch(0.90 0.008 80)',
  input: 'oklch(0.90 0.008 80)',
  ring: 'oklch(0.45 0.06 155)',
  // Sidebar — slightly darker warm surface
  sidebar: 'oklch(0.97 0.006 80)',
  'sidebar-foreground': 'oklch(0.22 0.01 60)',
  'sidebar-primary': 'oklch(0.35 0.06 155)',
  'sidebar-primary-foreground': 'oklch(0.98 0.005 85)',
  'sidebar-accent': 'oklch(0.94 0.008 80)',
  'sidebar-accent-foreground': 'oklch(0.22 0.01 60)',
  'sidebar-border': 'oklch(0.90 0.008 80)',
};

/**
 * Merge a partial course theme with the default theme
 * and apply the result as CSS custom properties on a DOM element.
 */
export function applyTheme(element: HTMLElement, overrides?: CourseTheme): void {
  const merged = { ...defaultTheme, ...overrides };
  for (const [key, value] of Object.entries(merged)) {
    element.style.setProperty(`--${key}`, value);
  }
}

/**
 * Remove all theme CSS custom properties from a DOM element,
 * allowing the stylesheet defaults to take effect again.
 */
export function clearTheme(element: HTMLElement): void {
  for (const key of Object.keys(defaultTheme)) {
    element.style.removeProperty(`--${key}`);
  }
}
