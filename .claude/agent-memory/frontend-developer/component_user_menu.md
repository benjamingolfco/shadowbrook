---
name: UserMenu component
description: Reusable avatar + dropdown for user info and sign out, placed in all layout headers
type: project
---

`UserMenu` lives at `src/components/layout/UserMenu.tsx`. It uses `Avatar` + `AvatarFallback` (initials) and `DropdownMenu` from shadcn/ui. Calls `useAuth()` from `@/features/auth` for `user` and `logout`.

Dropdown shows: displayName, email, role (capitalized), and a destructive "Sign out" item that calls `logout`.

**Placement per layout:**
- `AdminLayout` / `OperatorLayout` (sidebar): `UserMenu` appears in both `SidebarFooter` (desktop) and the mobile `<header>` inside `SidebarInset` (top-right, alongside `SidebarTrigger`)
- `GolferLayout`: top-right of the `<header>` (replaced the Profile NavLink)
- `WaitlistShellLayout`: top-right of the `<header>` (replaced inline user name + sign out button)

**shadcn components required:** `dropdown-menu` and `avatar` (added via `pnpm dlx shadcn@latest add dropdown-menu avatar`)
