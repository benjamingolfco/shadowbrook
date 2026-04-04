# Admin Organization Impersonation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow admin users to navigate to `/operator/*` routes and operate as any organization by selecting one from a dropdown, enabling full operator functionality scoped to the chosen org.

**Architecture:** The backend `UserContext.OrganizationId` gains an override path: when an admin sends an `X-Organization-Id` header, that value is used instead of the (null) claim. This flows through to the EF query filter, feature flags, and logging enricher transparently. The frontend adds an `OrgContext` (similar to the existing `CourseContext`) that stores the admin's selected org, injects the header via the API client, and enriches the `/auth/me` response with an `organizations` list for admins.

**Tech Stack:** .NET 10, EF Core, React 19, TypeScript, TanStack Query, Tailwind, shadcn/ui

---

## File Map

### Backend
- **Modify:** `src/backend/Shadowbrook.Api/Infrastructure/Auth/UserContext.cs` — read `X-Organization-Id` header for admins
- **Modify:** `src/backend/Shadowbrook.Api/Features/Auth/AuthEndpoints.cs` — include `organizations` list in `MeResponse` for admins
- **Test:** `tests/Shadowbrook.Api.Tests/Features/Auth/UserContextTests.cs` — unit tests for org override behavior

### Frontend
- **Create:** `src/web/src/features/operator/context/OrgContext.tsx` — org selection context for admin impersonation
- **Modify:** `src/web/src/lib/api-client.ts` — inject `X-Organization-Id` header when set
- **Modify:** `src/web/src/features/auth/types.ts` — add `organizations` to `MeResponse`
- **Modify:** `src/web/src/features/auth/hooks/useAuth.ts` — expose `organizations` in context
- **Modify:** `src/web/src/features/auth/providers/MsalAuthProvider.tsx` — pass `organizations` through
- **Modify:** `src/web/src/types/user.ts` — add `organizations` to `User`
- **Modify:** `src/web/src/features/operator/index.tsx` — wrap with `OrgProvider`, add `OrgGate` for admins
- **Modify:** `src/web/src/components/layout/OperatorLayout.tsx` — show org switcher dropdown for admins
- **Test:** `src/web/src/features/operator/__tests__/OrgContext.test.tsx` — unit tests for org context

---

## Task 1: Backend — `UserContext` reads org override header for admins

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Infrastructure/Auth/UserContext.cs:22-29`
- Test: `tests/Shadowbrook.Api.Tests/Features/Auth/UserContextTests.cs` (create)

- [ ] **Step 1: Write the failing test for admin org override**

Create `tests/Shadowbrook.Api.Tests/Features/Auth/UserContextTests.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shadowbrook.Api.Infrastructure.Auth;

namespace Shadowbrook.Api.Tests.Features.Auth;

public class UserContextTests
{
    private static UserContext CreateContext(
        ClaimsPrincipal? user = null,
        string? orgHeader = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null)
        {
            httpContext.User = user;
        }

        if (orgHeader is not null)
        {
            httpContext.Request.Headers["X-Organization-Id"] = orgHeader;
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        return new UserContext(accessor);
    }

    private static ClaimsPrincipal AdminUser(Guid appUserId)
    {
        var claims = new List<Claim>
        {
            new("app_user_id", appUserId.ToString()),
            new("role", "Admin"),
            new("permission", "app:access"),
            new("permission", "users:manage"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal OperatorUser(Guid appUserId, Guid orgId)
    {
        var claims = new List<Claim>
        {
            new("app_user_id", appUserId.ToString()),
            new("organization_id", orgId.ToString()),
            new("role", "Operator"),
            new("permission", "app:access"),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void OrganizationId_AdminWithHeader_ReturnsHeaderValue()
    {
        var targetOrgId = Guid.CreateVersion7();
        var context = CreateContext(
            user: AdminUser(Guid.CreateVersion7()),
            orgHeader: targetOrgId.ToString());

        Assert.Equal(targetOrgId, context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_AdminWithoutHeader_ReturnsNull()
    {
        var context = CreateContext(user: AdminUser(Guid.CreateVersion7()));

        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_OperatorWithHeader_IgnoresHeader()
    {
        var realOrgId = Guid.CreateVersion7();
        var fakeOrgId = Guid.CreateVersion7();
        var context = CreateContext(
            user: OperatorUser(Guid.CreateVersion7(), realOrgId),
            orgHeader: fakeOrgId.ToString());

        Assert.Equal(realOrgId, context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_AdminWithInvalidHeader_ReturnsNull()
    {
        var context = CreateContext(
            user: AdminUser(Guid.CreateVersion7()),
            orgHeader: "not-a-guid");

        Assert.Null(context.OrganizationId);
    }

    [Fact]
    public void OrganizationId_OperatorWithClaim_ReturnsClaim()
    {
        var orgId = Guid.CreateVersion7();
        var context = CreateContext(user: OperatorUser(Guid.CreateVersion7(), orgId));

        Assert.Equal(orgId, context.OrganizationId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~UserContextTests" --no-restore -v minimal`
Expected: `OrganizationId_AdminWithHeader_ReturnsHeaderValue` FAILS (returns null, not the header value)

- [ ] **Step 3: Implement the org override in `UserContext`**

Modify `src/backend/Shadowbrook.Api/Infrastructure/Auth/UserContext.cs`. Replace the `OrganizationId` property:

```csharp
public Guid? OrganizationId
{
    get
    {
        // Operators always use their claim-based org ID
        var claim = User?.FindFirst("organization_id");
        if (claim is not null && Guid.TryParse(claim.Value, out var claimOrgId))
        {
            return claimOrgId;
        }

        // Admins can override via header for impersonation
        if (IsAdmin)
        {
            var header = this.httpContextAccessor.HttpContext?.Request.Headers["X-Organization-Id"].FirstOrDefault();
            if (header is not null && Guid.TryParse(header, out var headerOrgId))
            {
                return headerOrgId;
            }
        }

        return null;
    }
}

private bool IsAdmin => User?.FindFirst("role")?.Value == "Admin";
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/Shadowbrook.Api.Tests --filter "FullyQualifiedName~UserContextTests" --no-restore -v minimal`
Expected: All 5 tests PASS

- [ ] **Step 5: Build to verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add tests/Shadowbrook.Api.Tests/Features/Auth/UserContextTests.cs src/backend/Shadowbrook.Api/Infrastructure/Auth/UserContext.cs
git commit -m "feat: allow admin org impersonation via X-Organization-Id header"
```

---

## Task 2: Backend — Add `organizations` list to `MeResponse` for admins

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Auth/AuthEndpoints.cs:44-76,184-192`

- [ ] **Step 1: Modify `GetMe` to include organizations for admins**

In `AuthEndpoints.cs`, add an organizations query for admin users inside the `GetMe` method, after the existing `org` variable (around line 42). Add the query:

```csharp
List<OrgResponse>? organizations = null;
if (appUser.Role == AppUserRole.Admin)
{
    organizations = await db.Organizations
        .OrderBy(o => o.Name)
        .Select(o => new OrgResponse(o.Id, o.Name))
        .ToListAsync();
}
```

Update the `MeResponse` record to include the new field:

```csharp
public sealed record MeResponse(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string Role,
    OrgResponse? Organization,
    List<OrgResponse>? Organizations,
    List<CourseResponse> Courses,
    List<string> Permissions);
```

Update the `MeResponse` construction in `GetMe` to pass the new field:

```csharp
var response = new MeResponse(
    appUser.Id,
    appUser.Email,
    appUser.FirstName,
    appUser.LastName,
    appUser.Role.ToString(),
    org,
    organizations,
    courses,
    userContext.Permissions.ToList());
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/Auth/AuthEndpoints.cs
git commit -m "feat: include organizations list in /auth/me for admin users"
```

---

## Task 3: Frontend — Add `organizations` to auth types and User model

**Files:**
- Modify: `src/web/src/features/auth/types.ts`
- Modify: `src/web/src/types/user.ts`
- Modify: `src/web/src/features/auth/hooks/useAuth.ts`
- Modify: `src/web/src/features/auth/providers/MsalAuthProvider.tsx`

- [ ] **Step 1: Add `organizations` to `MeResponse`**

In `src/web/src/features/auth/types.ts`, add `organizations`:

```typescript
export interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  role: AppUserRole;
  organization: { id: string; name: string } | null;
  organizations: { id: string; name: string }[] | null;
  courses: { id: string; name: string }[];
  permissions: string[];
}
```

- [ ] **Step 2: Add `organizations` to `User` type**

In `src/web/src/types/user.ts`:

```typescript
export interface User {
  id: string;
  email: string;
  displayName: string;
  role: string;
  organization: { id: string; name: string } | null;
  organizations: { id: string; name: string }[] | null;
  courses: { id: string; name: string }[];
  permissions: string[];
}
```

- [ ] **Step 3: Add `organizations` to `AuthContextValue`**

In `src/web/src/features/auth/hooks/useAuth.ts`, add to the interface:

```typescript
export interface AuthContextValue {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  unauthorized: boolean;
  permissions: string[];
  courses: { id: string; name: string }[];
  organizations: { id: string; name: string }[];
  login: () => void;
  logout: () => void;
  hasPermission: (permission: string) => boolean;
}
```

- [ ] **Step 4: Pass `organizations` through auth providers**

In `src/web/src/features/auth/providers/MsalAuthProvider.tsx`, update both `DevAuthProvider` and `MsalAuthContent`:

In the `user` useMemo (both providers), add:
```typescript
organizations: me.organizations,
```

In the `value` object (both providers), add:
```typescript
organizations: user?.organizations ?? [],
```

- [ ] **Step 5: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add src/web/src/features/auth/types.ts src/web/src/types/user.ts src/web/src/features/auth/hooks/useAuth.ts src/web/src/features/auth/providers/MsalAuthProvider.tsx
git commit -m "feat: expose organizations list in frontend auth context"
```

---

## Task 4: Frontend — Create `OrgContext` for admin org selection

**Files:**
- Create: `src/web/src/features/operator/context/OrgContext.tsx`

- [ ] **Step 1: Create `OrgContext`**

Create `src/web/src/features/operator/context/OrgContext.tsx`:

```typescript
import { createContext, useContext, useState, useCallback, type ReactNode } from 'react';

export interface SelectedOrg {
  id: string;
  name: string;
}

interface OrgContextValue {
  org: SelectedOrg | null;
  selectOrg: (org: SelectedOrg) => void;
  clearOrg: () => void;
}

const OrgContext = createContext<OrgContextValue | undefined>(undefined);

const STORAGE_KEY = 'shadowbrook-admin-org';

interface OrgProviderProps {
  children: ReactNode;
}

export function OrgProvider({ children }: OrgProviderProps) {
  const [org, setOrg] = useState<SelectedOrg | null>(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as SelectedOrg;
    } catch {
      return null;
    }
  });

  const selectOrg = useCallback((newOrg: SelectedOrg) => {
    setOrg(newOrg);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(newOrg));
  }, []);

  const clearOrg = useCallback(() => {
    setOrg(null);
    localStorage.removeItem(STORAGE_KEY);
  }, []);

  return (
    <OrgContext.Provider value={{ org, selectOrg, clearOrg }}>
      {children}
    </OrgContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useOrgContext() {
  const context = useContext(OrgContext);
  if (context === undefined) {
    throw new Error('useOrgContext must be used within an OrgProvider');
  }
  return context;
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/context/OrgContext.tsx
git commit -m "feat: add OrgContext for admin org selection"
```

---

## Task 5: Frontend — Inject `X-Organization-Id` header in API client

**Files:**
- Modify: `src/web/src/lib/api-client.ts`

The API client can't use React context (it's not a component), so we use a module-level getter that the `OrgProvider` sets.

- [ ] **Step 1: Add org ID header injection to `api-client.ts`**

Add a module-level org ID accessor at the top of the file (after the existing constants), and use it in the `request` function:

```typescript
// Admin org impersonation — set by OrgContext
let getAdminOrgId: (() => string | null) = () => null;

export function setAdminOrgIdGetter(getter: () => string | null) {
  getAdminOrgId = getter;
}
```

In the `request` function, after the authorization header is set (around line 51), add:

```typescript
const adminOrgId = getAdminOrgId();
if (adminOrgId) {
  headers['X-Organization-Id'] = adminOrgId;
}
```

- [ ] **Step 2: Wire up `OrgContext` to call `setAdminOrgIdGetter`**

In `src/web/src/features/operator/context/OrgContext.tsx`, import and call the setter:

Add at the top:
```typescript
import { setAdminOrgIdGetter } from '@/lib/api-client';
```

In `OrgProvider`, add a `useEffect` after the state initialization:

```typescript
import { createContext, useContext, useState, useCallback, useEffect, type ReactNode } from 'react';
```

```typescript
useEffect(() => {
  setAdminOrgIdGetter(() => org?.id ?? null);
  return () => setAdminOrgIdGetter(() => null);
}, [org]);
```

- [ ] **Step 3: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add src/web/src/lib/api-client.ts src/web/src/features/operator/context/OrgContext.tsx
git commit -m "feat: inject X-Organization-Id header for admin impersonation"
```

---

## Task 6: Frontend — Wrap operator feature with `OrgProvider` and add `OrgGate`

**Files:**
- Modify: `src/web/src/features/operator/index.tsx`

- [ ] **Step 1: Add OrgProvider and OrgGate to operator feature**

Replace the content of `src/web/src/features/operator/index.tsx`:

```typescript
import { useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router';
import OperatorLayout from '@/components/layout/OperatorLayout';
import WaitlistShellLayout from '@/components/layout/WaitlistShellLayout';
import TeeSheet from './pages/TeeSheet';
import TeeTimeSettings from './pages/TeeTimeSettings';
import WalkUpWaitlist from './pages/WalkUpWaitlist';
import CoursePortfolio from './pages/CoursePortfolio';
import OrgPicker from './pages/OrgPicker';
import { CourseProvider, useCourseContext } from './context/CourseContext';
import { OrgProvider, useOrgContext } from './context/OrgContext';
import { ThemeProvider } from '@/components/ThemeProvider';
import { useFeature } from '@/hooks/use-features';
import { useAuth } from '@/features/auth';

function OrgGate() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  if (isAdmin) {
    return <AdminOrgGate />;
  }

  return <CourseGate />;
}

function AdminOrgGate() {
  const { org } = useOrgContext();

  if (!org) {
    return (
      <Routes>
        <Route element={<OperatorLayout />}>
          <Route path="*" element={<OrgPicker />} />
        </Route>
      </Routes>
    );
  }

  return <CourseGate />;
}

function CourseGate() {
  const { course, clearCourse } = useCourseContext();
  const { courses } = useAuth();
  const fullOperatorApp = useFeature('full-operator-app', course?.id);

  useEffect(() => {
    if (course && !courses.some((c) => c.id === course.id)) {
      clearCourse();
    }
  }, [course, courses, clearCourse]);

  if (!course) {
    if (fullOperatorApp) {
      return (
        <Routes>
          <Route element={<OperatorLayout />}>
            <Route path="*" element={<CoursePortfolio />} />
          </Route>
        </Routes>
      );
    }

    return (
      <Routes>
        <Route element={<WaitlistShellLayout />}>
          <Route path="*" element={<CoursePortfolio />} />
        </Route>
      </Routes>
    );
  }

  if (!fullOperatorApp) {
    return (
      <Routes>
        <Route element={<WaitlistShellLayout />}>
          <Route path="*" element={<WalkUpWaitlist />} />
        </Route>
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<OperatorLayout />}>
        <Route path="tee-sheet" element={<TeeSheet />} />
        <Route path="waitlist" element={<WalkUpWaitlist />} />
        <Route path="settings" element={<TeeTimeSettings />} />
        <Route path="*" element={<Navigate to="tee-sheet" replace />} />
      </Route>
    </Routes>
  );
}

export default function OperatorFeature() {
  return (
    <ThemeProvider>
      <OrgProvider>
        <CourseProvider>
          <OrgGate />
        </CourseProvider>
      </OrgProvider>
    </ThemeProvider>
  );
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors (OrgPicker page doesn't exist yet — will be created in next task)

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/index.tsx
git commit -m "feat: add OrgGate for admin impersonation flow"
```

---

## Task 7: Frontend — Create `OrgPicker` page for admins

**Files:**
- Create: `src/web/src/features/operator/pages/OrgPicker.tsx`

- [ ] **Step 1: Create `OrgPicker` page**

Create `src/web/src/features/operator/pages/OrgPicker.tsx`:

```typescript
import { useAuth } from '@/features/auth';
import { useOrgContext } from '../context/OrgContext';
import { Card, CardContent } from '@/components/ui/card';

export default function OrgPicker() {
  const { organizations } = useAuth();
  const { selectOrg } = useOrgContext();

  return (
    <div className="p-6">
      <h2 className="text-lg font-semibold mb-4">Select an organization</h2>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {organizations.map((org) => (
          <Card
            key={org.id}
            className="cursor-pointer transition-colors hover:bg-accent"
            onClick={() => selectOrg({ id: org.id, name: org.name })}
          >
            <CardContent className="p-4">
              <span className="font-medium">{org.name}</span>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/pages/OrgPicker.tsx
git commit -m "feat: add OrgPicker page for admin org selection"
```

---

## Task 8: Frontend — Add org switcher to OperatorLayout header for admins

**Files:**
- Modify: `src/web/src/components/layout/OperatorLayout.tsx`

- [ ] **Step 1: Add org switcher dropdown for admins**

Replace the content of `src/web/src/components/layout/OperatorLayout.tsx`:

```typescript
import { NavLink, Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarProvider,
  SidebarInset,
  SidebarTrigger,
} from '@/components/ui/sidebar';
import { Badge } from '@/components/ui/badge';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ChevronsUpDown } from 'lucide-react';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useOrgContext } from '@/features/operator/context/OrgContext';
import UserMenu from '@/components/layout/UserMenu';

function OrgSwitcher() {
  const { organizations } = useAuth();
  const { org, selectOrg, clearOrg } = useOrgContext();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const handleSelect = useCallback(
    (selected: { id: string; name: string }) => {
      clearCourse();
      selectOrg({ id: selected.id, name: selected.name });
      navigate('/operator');
    },
    [clearCourse, selectOrg, navigate],
  );

  const handleClear = useCallback(() => {
    clearCourse();
    clearOrg();
    navigate('/operator');
  }, [clearCourse, clearOrg, navigate]);

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-1 text-lg font-semibold font-[family-name:var(--font-heading)] hover:bg-accent rounded-md px-1 -mx-1"
        >
          <span className="max-w-[180px] truncate" title={org?.name ?? 'Select org'}>
            {org?.name ?? 'Select org'}
          </span>
          <ChevronsUpDown className="h-4 w-4 shrink-0 opacity-50" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        {organizations.map((o) => (
          <DropdownMenuItem
            key={o.id}
            onSelect={() => handleSelect(o)}
            className={o.id === org?.id ? 'bg-accent' : ''}
          >
            {o.name}
          </DropdownMenuItem>
        ))}
        {org && (
          <DropdownMenuItem onSelect={handleClear} className="text-muted-foreground">
            Back to org list
          </DropdownMenuItem>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default function OperatorLayout() {
  const { user } = useAuth();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();
  const isAdmin = user?.role === 'Admin';

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <div className="flex items-center gap-2 py-2">
            {isAdmin ? (
              <OrgSwitcher />
            ) : (
              <h1
                className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)]"
                title={user?.organization?.name ?? 'Shadowbrook'}
              >
                {user?.organization?.name ?? 'Shadowbrook'}
              </h1>
            )}
            <Badge variant={isAdmin ? 'default' : 'success'} className="text-[10px] px-1.5 py-0">
              {isAdmin ? 'Admin' : 'Operator'}
            </Badge>
          </div>
        </SidebarHeader>
        <SidebarContent>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/tee-sheet">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Tee Sheet</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/waitlist">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Waitlist</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <NavLink to="/operator/settings">
                  {({ isActive }) => (
                    <span className={isActive ? 'font-semibold' : ''}>Settings</span>
                  )}
                </NavLink>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarContent>
      </Sidebar>
      <SidebarInset>
        <header className="flex h-12 items-center border-b px-4">
          <SidebarTrigger className="md:hidden" />
          <div className="ml-auto">
            <UserMenu onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined} />
          </div>
        </header>
        <Outlet />
      </SidebarInset>
    </SidebarProvider>
  );
}
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add src/web/src/components/layout/OperatorLayout.tsx
git commit -m "feat: add org switcher dropdown to OperatorLayout for admins"
```

---

## Task 9: Frontend — Scope `/auth/me` courses to selected org for admins

When an admin selects an org and the `X-Organization-Id` header is active, the backend's EF query filter will scope courses. But `/auth/me` currently uses `IgnoreQueryFilters()` for admins (line 47-50 of `AuthEndpoints.cs`). We need to refine this so that when an admin sends the org header, courses are filtered to that org.

**Files:**
- Modify: `src/backend/Shadowbrook.Api/Features/Auth/AuthEndpoints.cs:44-64`

- [ ] **Step 1: Update `GetMe` to filter courses by org header for admins**

In the `GetMe` method, replace the admin courses block:

```csharp
if (appUser.Role == AppUserRole.Admin)
{
    if (userContext.OrganizationId is { } adminOrgId)
    {
        courses = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.OrganizationId == adminOrgId)
            .Select(c => new CourseResponse(c.Id, c.Name))
            .ToListAsync();
    }
    else
    {
        courses = await db.Courses
            .IgnoreQueryFilters()
            .Select(c => new CourseResponse(c.Id, c.Name))
            .ToListAsync();
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/backend/Shadowbrook.Api/Features/Auth/AuthEndpoints.cs
git commit -m "feat: scope admin /auth/me courses to impersonated org"
```

---

## Task 10: Frontend — Invalidate `/auth/me` when org changes

When the admin switches orgs, the cached `MeResponse` has stale courses. We need to invalidate the `['auth', 'me']` query when the org selection changes.

**Files:**
- Modify: `src/web/src/features/operator/context/OrgContext.tsx`

- [ ] **Step 1: Invalidate auth query on org change**

In `OrgContext.tsx`, import `useQueryClient` and invalidate on change:

Add import:
```typescript
import { useQueryClient } from '@tanstack/react-query';
```

In `OrgProvider`, add:
```typescript
const queryClient = useQueryClient();
```

Update `selectOrg`:
```typescript
const selectOrg = useCallback((newOrg: SelectedOrg) => {
  setOrg(newOrg);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(newOrg));
  void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
}, [queryClient]);
```

Update `clearOrg`:
```typescript
const clearOrg = useCallback(() => {
  setOrg(null);
  localStorage.removeItem(STORAGE_KEY);
  void queryClient.invalidateQueries({ queryKey: ['auth', 'me'] });
}, [queryClient]);
```

- [ ] **Step 2: Lint**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add src/web/src/features/operator/context/OrgContext.tsx
git commit -m "feat: invalidate /auth/me cache on org switch"
```

---

## Task 11: Smoke test — Run `make dev` and verify end-to-end

- [ ] **Step 1: Build backend**

Run: `dotnet build shadowbrook.slnx`
Expected: Build succeeded

- [ ] **Step 2: Lint frontend**

Run: `pnpm --dir src/web lint`
Expected: No errors

- [ ] **Step 3: Run backend tests**

Run: `make test`
Expected: All tests pass

- [ ] **Step 4: Run frontend tests**

Run: `pnpm --dir src/web test`
Expected: All tests pass

- [ ] **Step 5: Run `make dev` and verify**

Run: `make dev`

Manual verification:
1. Log in as an admin user
2. Navigate to `/operator` — should see `OrgPicker` with list of organizations
3. Select an organization — should see the operator layout with the org name in sidebar dropdown
4. Select a course — should see the tee sheet scoped to that org
5. Click the org dropdown — should be able to switch to a different org (course resets)
6. Log in as an operator — should see normal operator flow with no org switcher
