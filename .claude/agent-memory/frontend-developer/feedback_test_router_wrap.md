---
name: Test Router Wrapping for Route-Driven Providers
description: When testing providers that call useCourseId (or any useParams hook), don't use test-utils render — build a custom wrapper with QueryClient + MemoryRouter + Routes + Route path="/.../:param"
type: feedback
---

When a provider calls `useCourseId()` (or any hook that calls `useParams()`), tests must render it inside a real `<Routes><Route path="/.../:courseId" element={...}>` — not via `render` from `@/test/test-utils`, which wraps in its own `MemoryRouter` and causes a "cannot render a Router inside another Router" error.

**Pattern to use:**

```tsx
function createWrapper(initialRoute = '/courses/course-123') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter initialEntries={[initialRoute]}>
          <Routes>
            <Route path="/courses/:courseId" element={<CourseProvider>{children}</CourseProvider>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    );
  };
}
// Then: render(<CourseDisplay />, { wrapper: Wrapper })
```

**Why:** `test-utils.render` always wraps in `MemoryRouter`. Nesting a second router inside it throws. Route-param hooks also throw if there's no matching route with the expected param.

**How to apply:** Any time you write tests for a context provider that internally calls `useCourseId` or any `useParams`-dependent hook, build the wrapper manually rather than using `test-utils.render`.
