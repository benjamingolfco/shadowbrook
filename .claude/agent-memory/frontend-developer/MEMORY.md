# Frontend Developer Memory

## Project Quick Reference

- **Web source:** `src/web/src/`
- **Feature pattern:** `features/{name}/pages/`, `components/`, `hooks/`, `__tests__/`
- **Router:** `src/web/src/app/router.tsx` — role-guarded lazy routes
- **Test utils:** `src/web/src/test/test-utils.tsx` — wraps QueryClient + MemoryRouter
- **API client:** `src/web/src/lib/api-client.ts` — `api.get/post/put/delete`, omits X-Tenant-Id when null

## Key Conventions

### Imports
- Zod: `import { z } from 'zod/v4'` (not `'zod'`) per react-conventions.md
- React Router: `import { ... } from 'react-router'` (not `react-router-dom`)
- Path alias: `@/*` maps to `src/*`

### Forms
- React Hook Form + Zod + shadcn Form components (`Form`, `FormField`, `FormItem`, `FormLabel`, `FormControl`, `FormMessage`)
- Pattern: `useForm({ resolver: zodResolver(schema) })`

### Mutations (TanStack Query)
- `useMutation({ mutationFn: ... })` — no query keys needed for mutations
- Call as `mutation.mutate(data, { onSuccess, onError })` in components
- Inline callbacks in `.mutate()` are fine for one-shot flows

### Testing Pattern
- `vi.mock('../hooks/useXxx')` then `vi.mocked(useXxx)` for typed mock
- `mockHook.mockReturnValue({ ... } as unknown as ReturnType<typeof useXxx>)`
- `userEvent` from `@testing-library/user-event`, `render/screen/waitFor` from `@/test/test-utils`
- `vi.clearAllMocks()` in `beforeEach`

## Routing

- Public routes: add to router.tsx **without** AuthGuard/RoleGuard, just `<LazyFeature>`
- Auth-guarded routes: wrap in `<AuthGuard><RoleGuard allowedRoles={[...]}>`
- Lazy-load features: `const XFeature = lazy(() => import('@/features/x'))`

## Components Added (issue/31)

### walkup feature (`src/web/src/features/walkup/`)
- `index.tsx` — entry point, renders WalkUpJoin
- `pages/WalkUpJoin.tsx` — 3-phase state machine (code → join → confirmed)
- `components/CodeEntry.tsx` — 4-digit numeric input, auto-submit on 4th digit
- `components/JoinForm.tsx` — name + phone form, RHF + Zod
- `components/Confirmation.tsx` — green check, position, personalized message
- `hooks/useVerifyCode.ts` — POST /walkup/verify mutation
- `hooks/useJoinWaitlist.ts` — POST /walkup/join mutation

## Patterns Discovered

### State Machine Pattern
Use TypeScript discriminated union for multi-phase pages:
```typescript
type Phase =
  | { step: 'code' }
  | { step: 'join'; courseWaitlistId: string; courseName: string }
  | { step: 'confirmed'; firstName: string; position: number; isExisting: boolean };
```
Then `useState<Phase>({ step: 'code' })` and conditional rendering on `phase.step`.

### Public/Unauthenticated Route Pattern
Routes that bypass auth: place in router.tsx outside auth guards, use `<LazyFeature>` only.
The api-client correctly omits X-Tenant-Id when activeTenantId is null — no special handling needed.

### Error Status Handling
The api-client throws `Error & { status?: number }` for non-OK responses.
Cast in components: `const errorWithStatus = err as Error & { status?: number }`.

## Build/Lint Notes

- node_modules may not be installed in CI sandbox — lint/test must run via `make lint` / `make test`
- pnpm available via `/usr/local/lib/node_modules/corepack/shims/pnpm` if corepack is enabled
- Test command: `pnpm --dir src/web test`
- Lint command: `pnpm --dir src/web lint`
