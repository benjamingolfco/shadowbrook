---
paths:
  - "src/web/**/*.{ts,tsx}"
---

# Frontend React Conventions

## Project Structure

Feature-based organization. Each feature is self-contained with co-located pages, components, and hooks.

```
src/web/src/
├── app/              # App shell: App.tsx, providers.tsx, router.tsx
├── components/
│   ├── ui/           # shadcn/ui primitives (managed by shadcn CLI)
│   └── layout/       # Role-based layouts (AdminLayout, OperatorLayout, GolferLayout)
├── features/
│   ├── admin/        # Platform admin (pages/, hooks/, components/, __tests__/)
│   ├── operator/     # Course operator
│   ├── golfer/       # Golfer-facing
│   └── auth/         # Auth provider, guards, dev switcher
├── hooks/            # Shared hooks (useMediaQuery, useDebounce, etc.)
├── lib/              # api-client.ts, query-keys.ts, utils.ts
├── types/            # Shared TypeScript interfaces
└── test/             # Test setup and utilities
```

**Import rules:**
- Features import from `@/components/ui/`, `@/lib/`, `@/hooks/`, `@/types/`
- Features NEVER import from other features — shared logic goes in `lib/` or `hooks/`
- Use `@/*` path alias for all imports (maps to `src/*`)

## Data Fetching

Use TanStack Query for all server state. Never use raw `useEffect` + `useState` for API calls.

```typescript
// Hook in features/{feature}/hooks/
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api-client';
import { queryKeys } from '@/lib/query-keys';

export function useCourses() {
  return useQuery({
    queryKey: queryKeys.courses.all,
    queryFn: () => api.get<Course[]>('/courses'),
  });
}
```

- API client: `@/lib/api-client.ts` — thin `fetch` wrapper with `api.get/post/put/delete`
- Query keys: `@/lib/query-keys.ts` — centralized factory for cache invalidation
- Mutations use `useMutation` with `onSuccess` invalidation via `queryClient.invalidateQueries`

## Forms

React Hook Form + Zod validation + shadcn Form components.

```typescript
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';

const schema = z.object({
  name: z.string().min(1, 'Required'),
  email: z.union([z.string().email('Invalid email'), z.literal('')]),
});

const form = useForm({ resolver: zodResolver(schema) });
```

- Zod schemas live alongside their feature
- Use shadcn `<Form>`, `<FormField>`, `<FormItem>`, `<FormLabel>`, `<FormControl>`, `<FormMessage>`
- Zod v4: import from `zod/v4` (not `zod`)

## Styling

Tailwind CSS utility classes. No CSS modules, no styled-components, no inline style objects.

- Use shadcn/ui primitives from `@/components/ui/` for standard UI elements
- Use `cn()` from `@/lib/utils` to merge conditional Tailwind classes
- shadcn components are owned source files (not a node_module) — edit them freely
- Add new shadcn components via `pnpm dlx shadcn@latest add <component>`

## Routing

React Router v7. Import from `react-router` (NOT `react-router-dom`).

- Routes defined in `app/router.tsx`
- Three role-based route groups: `/admin/*`, `/operator/*`, `/golfer/*`
- `AuthGuard` checks authentication, `RoleGuard` checks role authorization
- Use `NavLink` for navigation items (provides `isActive` for styling)
- Use `Link` for general navigation

## Auth

Mock provider for dev, MSAL for production. Same `useAuth()` interface.

- `useAuth()` hook from `@/features/auth` — returns `{ user, role, isAuthenticated, login, logout, setRole }`
- `MockAuthProvider` stores role in localStorage, always authenticated
- `DevRoleSwitcher` floats bottom-left in dev mode to cycle roles
- Never import auth internals directly — use the barrel export from `@/features/auth`

## Testing

Vitest + React Testing Library.

- Test files: `features/{feature}/__tests__/*.test.tsx`
- Use `render` from `@/test/test-utils` (wraps with QueryClient + MemoryRouter)
- Mock hooks with `vi.mock()` + `vi.mocked()` — cast partial returns as `unknown`
- Run: `pnpm --dir src/web test` (single run) or `pnpm --dir src/web test:watch`

## Component Patterns

- Pages are default exports in `features/{feature}/pages/`
- Hooks are named exports in `features/{feature}/hooks/`
- Loading/error/empty states are handled in the page component, not the hook
- Use shadcn `Table` for tabular data, `Form` for forms, `Button` for actions
