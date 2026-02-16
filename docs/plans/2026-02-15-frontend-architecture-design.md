# Frontend Architecture Design

## Decisions

| Concern | Decision |
|---|---|
| App structure | Single app, role-based routes. Golfer section designed for future extraction into standalone app. |
| Routing | React Router (standard client-side routing) |
| UI components | shadcn/ui (copy-paste primitives built on Radix UI) |
| Styling | Tailwind CSS |
| Data fetching | TanStack Query |
| Forms | React Hook Form + Zod validation |
| Auth | Mock provider now (dev role switcher), Azure AD B2C + MSAL later. Same interface, one provider swap. |
| Golfer v1 scope | Browse tee times, book, profile (SMS for notifications only) |
| Testing | Vitest + React Testing Library |
| Code organization | Feature-based folders |

## Project Structure

```
src/web/src/
├── app/
│   ├── App.tsx                 # Root component: providers, router outlet
│   ├── providers.tsx           # Compose all providers (QueryClient, Auth, Theme)
│   └── router.tsx              # Route definitions with lazy imports
│
├── features/
│   ├── admin/
│   │   ├── pages/              # CourseList, CourseCreate, CourseDetail
│   │   ├── components/         # Admin-specific components
│   │   └── hooks/              # Admin-specific hooks & queries
│   │
│   ├── operator/
│   │   ├── pages/              # TeeSheet, TeeTimeSettings, CourseSettings
│   │   ├── components/         # Operator-specific components
│   │   └── hooks/              # Operator-specific hooks & queries
│   │
│   ├── golfer/
│   │   ├── pages/              # BrowseTeeTimes, Booking, Profile, MyBookings
│   │   ├── components/         # Golfer-specific components
│   │   └── hooks/              # Golfer-specific hooks & queries
│   │
│   └── auth/
│       ├── pages/              # Login, Register, Callback (future)
│       ├── components/         # AuthGuard, RoleGuard
│       └── hooks/              # useAuth, useCurrentUser
│
├── components/
│   ├── ui/                     # shadcn/ui primitives (button, dialog, table, etc.)
│   └── layout/                 # AppShell, Sidebar, Header, ErrorBoundary
│
├── hooks/                      # Shared hooks (useMediaQuery, useDebounce, etc.)
├── lib/
│   ├── api-client.ts           # Configured fetch wrapper with base URL and JSON handling
│   ├── query-keys.ts           # TanStack Query key factory
│   └── utils.ts                # Shared utilities (cn helper, date formatting, etc.)
│
├── types/                      # Shared TypeScript types/interfaces
│   ├── course.ts
│   ├── tee-time.ts
│   ├── booking.ts
│   └── user.ts
│
├── main.tsx                    # Entry point: createRoot → <App />
└── index.css                   # Tailwind directives + global resets
```

**Rules:**
- Each feature folder is self-contained. Its pages, components, and hooks are co-located.
- Features may import from `components/ui/`, `lib/`, `hooks/`, and `types/`.
- Features must NOT import from other features. Shared logic goes in `lib/` or `hooks/`.
- `components/ui/` is managed by the shadcn CLI — these are owned source files, not a node_module.

## Routing

Three top-level route groups, each with its own layout:

```
/auth/*               → Minimal layout (future: login, register, B2C callback)
/admin/*              → AdminLayout (sidebar nav, full-width content)
/operator/*           → OperatorLayout (course-scoped top nav)
/golfer/*             → GolferLayout (mobile-first, bottom tab bar)
/                     → Redirect based on role
```

### Route Definitions

```
/admin/courses              ← Course list
/admin/courses/new          ← Create course
/admin/courses/:id          ← Course detail/edit

/operator/tee-sheet         ← Tee sheet view
/operator/settings          ← Tee time settings
/operator/waitlist          ← Waitlist management (future)
/operator/bookings          ← Booking management (future)

/golfer/tee-times           ← Browse available tee times
/golfer/book/:slotId        ← Booking flow
/golfer/bookings            ← My upcoming bookings
/golfer/profile             ← Profile, handicap, payment
```

### Key Patterns

- **Lazy loading per feature:** `React.lazy(() => import('./features/admin/pages/...'))`. Admin and operator code is not shipped to golfer bundles.
- **Route guards:** `AuthGuard` checks for a valid session. `RoleGuard` checks the user's role (admin, operator, golfer) and redirects if unauthorized.
- **SWA fallback:** `staticwebapp.config.json` with `navigationFallback: { rewrite: "/index.html" }` for direct URL access and refresh.

## Auth (Mock Now, Real Later)

### Interface

```typescript
interface AuthContext {
  user: User | null;
  role: 'admin' | 'operator' | 'golfer';
  isAuthenticated: boolean;
  login: () => void;
  logout: () => void;
}
```

### Mock Provider (Now)

`MockAuthProvider` stores the current role in localStorage. A dev-only role switcher widget (floating in corner, hidden in production) lets you flip between admin, operator, and golfer instantly. No login screen, no tokens.

Route guards still enforce access — a mock "golfer" cannot navigate to `/admin/*`.

### Real Provider (Later)

Swap `MockAuthProvider` for `MsalAuthProvider` wrapping `@azure/msal-react`. The `useAuth` hook signature stays the same. Role comes from B2C token claims instead of localStorage. One provider swap, everything else works.

## Layouts

### AdminLayout

Full sidebar navigation, wide content area. For system-level management.

```
┌──────────┬─────────────────────────┐
│ Sidebar  │                         │
│          │   Content area          │
│ Courses  │                         │
│ Users    │                         │
│ Settings │                         │
└──────────┴─────────────────────────┘
```

### OperatorLayout

Top nav scoped to the operator's course. Focused on daily operations.

```
┌─────────────────────────────────────┐
│ Course Name    TeeSheet | Settings  │
├─────────────────────────────────────┤
│                                     │
│   Content area                      │
│                                     │
└─────────────────────────────────────┘
```

### GolferLayout

Clean, mobile-first. Bottom tab bar navigation.

```
┌─────────────────────────────────────┐
│ Shadowbrook                 Profile │
├─────────────────────────────────────┤
│                                     │
│   Content area                      │
│                                     │
├─────────────────────────────────────┤
│  Tee Times  |  My Bookings         │
└─────────────────────────────────────┘
```

## Data Fetching

### API Client

Thin wrapper around `fetch` in `lib/api-client.ts`:

```typescript
const api = {
  get<T>(path: string): Promise<T>,
  post<T>(path: string, body: unknown): Promise<T>,
  put<T>(path: string, body: unknown): Promise<T>,
  delete(path: string): Promise<void>,
}
```

Handles base URL, JSON serialization, error handling. When real auth arrives, add bearer token from MSAL in one place.

### TanStack Query

Each feature defines its own query hooks:

```typescript
// features/operator/hooks/useTeeSheet.ts
export function useTeeSheet(courseId: string, date: string) {
  return useQuery({
    queryKey: queryKeys.teeSheets.byDate(courseId, date),
    queryFn: () => api.get(`/tee-sheets?courseId=${courseId}&date=${date}`),
  });
}
```

### Query Key Factory

Centralized in `lib/query-keys.ts` for consistent cache invalidation:

```typescript
export const queryKeys = {
  courses: {
    all: ['courses'] as const,
    detail: (id: string) => ['courses', id] as const,
    settings: (id: string) => ['courses', id, 'settings'] as const,
  },
  teeSheets: {
    byDate: (courseId: string, date: string) => ['tee-sheets', courseId, date] as const,
  },
}
```

Mutations use `useMutation` with `onSuccess` invalidation.

## Forms & Validation

React Hook Form for state, Zod for validation, shadcn/ui `<Form>` for rendering:

```typescript
const courseSchema = z.object({
  name: z.string().min(1, "Course name is required"),
  phoneNumber: z.string().regex(/^\+?[\d\s-()]+$/, "Invalid phone number"),
  numberOfHoles: z.enum(["9", "18"]),
});

type CourseForm = z.infer<typeof courseSchema>;
```

Zod schemas live alongside their feature (e.g., `features/operator/schemas/`).

## Migration Plan

Existing components move into the new structure:

| Current File | New Location | Changes |
|---|---|---|
| `AdminCourses.tsx` | `features/admin/pages/CourseList.tsx` | TanStack Query, shadcn Table |
| `CourseRegistration.tsx` | `features/admin/pages/CourseCreate.tsx` | React Hook Form + Zod, shadcn Form |
| `TeeTimeSettings.tsx` | `features/operator/pages/TeeTimeSettings.tsx` | React Hook Form + Zod, TanStack Query |
| `TeeSheet.tsx` | `features/operator/pages/TeeSheet.tsx` | TanStack Query, shadcn Table + date picker |

All four are small enough (<150 lines each) that rewriting with new patterns is cleaner than incremental refactoring.

**Deleted:**
- `App.css` — replaced by Tailwind utilities
- `api.ts` — replaced by `lib/api-client.ts`
- Manual view switching in `App.tsx` — replaced by React Router

## TypeScript Config Updates

The current tsconfig is solid but needs a few updates for the new architecture.

### Changes to `tsconfig.app.json`

| Setting | Current | Recommended | Why |
|---|---|---|---|
| `useDefineForClassFields` | `true` | Remove | Redundant — this is the default when target >= ES2022 |
| `isolatedModules` | Missing | `true` | Required for Vite's esbuild transpilation to work correctly |
| `resolveJsonModule` | Missing | `true` | Allows importing JSON files (package metadata, config, etc.) |
| `noImplicitReturns` | Missing | `true` | Catch missing return statements in functions |
| `noImplicitOverride` | Missing | `true` | Require `override` keyword for class method overrides |
| `noUncheckedIndexedAccess` | Missing | `true` | Array/object index access returns `T \| undefined` — catches common bugs |
| Path aliases | Missing | `"@/*": ["src/*"]` | Clean imports: `import { Button } from '@/components/ui/button'` |

### Path Aliases

Add to `tsconfig.app.json`:
```json
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@/*": ["src/*"]
    }
  }
}
```

Add matching alias in `vite.config.ts`:
```typescript
import path from 'path';

export default defineConfig({
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
});
```

### `tsconfig.node.json`

No changes needed. ES2023 target is appropriate for the Vite config file.

## New Dependencies

| Package | Purpose |
|---|---|
| `react-router` | Client-side routing |
| `@tanstack/react-query` | Server state management, caching |
| `@tanstack/react-query-devtools` | Dev tools for inspecting queries (dev only) |
| `react-hook-form` | Form state management |
| `@hookform/resolvers` | Connects React Hook Form to Zod |
| `zod` | Schema validation |
| `tailwindcss` | Utility-first CSS framework |
| `@tailwindcss/vite` | Tailwind Vite plugin |
| `class-variance-authority` | Component variant styling (used by shadcn/ui) |
| `clsx` | Conditional class names |
| `tailwind-merge` | Merge Tailwind classes without conflicts |
| `lucide-react` | Icon library (used by shadcn/ui) |
| `vitest` | Test runner (Vite-native) |
| `@testing-library/react` | Component testing utilities |
| `@testing-library/jest-dom` | DOM assertion matchers |
| `jsdom` | DOM environment for tests |

## SWA Configuration

Add `src/web/staticwebapp.config.json`:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*"]
  }
}
```

This ensures direct URL navigation and page refresh work with client-side routing.
