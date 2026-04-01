---
name: Vitest async mock factory deadlock
description: Using async vi.mock factories to import react-router (or modules already in the test's import graph) causes a hang
type: feedback
---

Async `vi.mock` factories that call `await import('react-router')` or `await import(module-already-in-graph)` deadlock vitest — the test process hangs indefinitely with no output.

**Why:** Vitest hoists `vi.mock` calls before imports run. When the async factory tries to import a module that is itself part of the test's dependency graph (e.g., `react-router`), the module resolution deadlocks because the graph is partially resolved.

**How to apply:**
- Never use `async () => { const x = await import('react-router'); ... }` in a `vi.mock` factory
- To get `Outlet` or other react-router exports in a layout mock, either:
  1. Skip rendering `Outlet` in the mock entirely and assert on the layout wrapper's `data-testid` instead (preferred when you just want to verify _which_ layout rendered)
  2. Use `vi.mock('module', async (importOriginal) => { ... })` only when mocking that exact module and needing its original exports — `importOriginal` fetches the original of the _mocked_ module, not other modules
- The `importOriginal` pattern (`vi.mock('@tanstack/react-query', async (importOriginal) => { const actual = await importOriginal(); ... })`) is safe for spread-and-override patterns — confirmed working in CourseSwitcher.test.tsx
