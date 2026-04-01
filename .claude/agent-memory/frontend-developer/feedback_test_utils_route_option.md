---
name: test-utils route option
description: test-utils customRender supports a route option to set MemoryRouter initialEntries
type: feedback
---

`src/web/src/test/test-utils.tsx` was extended to accept a `route` string option that sets `initialEntries` on the `MemoryRouter`. Use `render(<Component />, { route: '/some/path' })` to test route-sensitive components.

**Why:** Components that render internal `<Routes>` (like `OperatorFeature`) need the MemoryRouter to start at a specific path to match the correct child routes.

**How to apply:** Pass `{ route: '/operator/waitlist' }` as the second arg to `render` when the component under test uses React Router and needs a specific initial URL.
