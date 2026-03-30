---
name: ESLint react-refresh/only-export-components pattern
description: Provider files that export both a component and a hook need eslint-disable-next-line on the hook export
type: feedback
---

When a file exports both a React component and a non-component (hook, constant, context accessor), the `react-refresh/only-export-components` rule will error.

Add `// eslint-disable-next-line react-refresh/only-export-components` on the line immediately before the non-component export.

**Why:** The project enforces this rule for fast refresh reliability. Co-locating a `useFoo()` hook with its provider component is an accepted pattern, but the suppress comment must be explicit.

**How to apply:** Any time a file exports both a component and a hook (e.g., `ThemeProvider` + `useTheme`, context providers + their accessor hooks). Run `pnpm --dir src/web lint` before committing to catch this.
