# Operator/Admin Redesign — Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the Fieldstone design language and a unified `AppShell` (full + minimal variants) for Teeforce's operator/admin surfaces, with the operator tee sheet redesigned as the proof page. All other pages get migrated in follow-up cluster PRs (out of scope for this plan).

**Architecture:** Theme shadcn primitives entirely through CSS variables in `index.css` so primitives stay stock. Build a thin `AppShell` that composes shadcn's `Sidebar` primitive plus a slot mechanism (`PageTopbar` / `PageRightRail`) backed by React portals so pages can contribute topbar and right-rail content from inside `<Outlet>`. Existing layouts (`OperatorLayout`, `AdminLayout`, `WaitlistShellLayout`) become thin wrappers around `AppShell` so the router does not move.

**Tech Stack:** React 19, TypeScript, Tailwind CSS v4 (`@theme inline` block in `index.css` — no separate config file), shadcn/ui (vendored), TanStack Query, React Router v7, Vitest + RTL.

**Spec:** `docs/superpowers/specs/2026-04-06-operator-admin-redesign-foundation-design.md`

**User-instructed deviations from default workflow:**
- **No new unit tests.** Existing tests must keep passing; only update locators where the redesign forces a change. Do not write TDD-style failing tests for new components.
- **No new functionality.** Visual / structural only. No new endpoints, no new aggregations, no new fields on existing models.
- **Right rail is hideable, not collapsible.** No user toggle. Pages render `<PageRightRail>` to populate it; absence means no rail.

**Refinements to spec discovered during planning:**
- The current `OperatorLayout` already uses shadcn's `<Sidebar>` primitive (`SidebarProvider`, `SidebarHeader`, `SidebarContent`, `SidebarMenu`, `SidebarInset`). The plan reuses that primitive — `AppShell` composes shadcn's `Sidebar` and themes it via the `--sidebar` / `--sidebar-foreground` tokens, rather than building a custom sidebar from scratch. Same outcome, far less code, responsive collapse preserved.
- `--sidebar` and friends are mapped to the Fieldstone ink palette so the dark sidebar look comes from token reassignment, not source edits.
- Tailwind v4 — no `tailwind.config.ts` exists. All theme additions go in the `@theme inline` block of `src/web/src/index.css`.
- Admin users currently see an `OrgSwitcher` dropdown in the sidebar header (not a "course selector"). It is preserved as-is — only restyled. Non-admin users see the static org name as today.
- No tee sheet e2e tests exist today (`src/web/e2e/tests/walkup` is the only test folder). Spec's mention of tee sheet e2e locator updates is dropped.
- `PageHeader` already uses `font-[family-name:var(--font-heading)]` — restyling it is just changing what `--font-heading` resolves to, which happens in token tasks. No source edits to `PageHeader.tsx` are required.

---

## File Structure

### New files

```
src/web/src/
├── components/
│   ├── layout/
│   │   ├── AppShell.tsx                  # The unified shell, two variants
│   │   ├── AppShellContext.tsx           # Portal target context for slot mechanism
│   │   ├── PageTopbar.tsx                # Slot helper: portals into topbar
│   │   ├── PageRightRail.tsx             # Slot helper: portals into right rail
│   │   └── PanelSection.tsx              # Right-rail section wrapper primitive
│   └── ui/
│       ├── status-badge.tsx              # StatusBadge wrapper around shadcn Badge
│       └── status-chip.tsx               # StatusChip primitive (dot-pill)
└── features/
    ├── admin/
    │   └── navigation.ts                 # adminNav config
    └── operator/
        ├── navigation.ts                 # operatorNav config
        ├── components/
        │   ├── PlayerCell.tsx
        │   ├── EmptySlot.tsx
        │   ├── NowMarker.tsx
        │   ├── PlayerAvatar.tsx
        │   ├── TeeSheetGrid.tsx
        │   ├── TeeSheetRow.tsx
        │   ├── TeeSheetTopbarTitle.tsx
        │   ├── TeeSheetDateNav.tsx
        │   └── teeSheetHelpers.ts        # mapTeeTimeStatus, getInitials
```

### Modified files

```
src/web/index.html                                  # Swap fonts to Plex + Baskerville
src/web/src/index.css                               # Fieldstone palette + sidebar dark mapping
src/web/src/components/layout/OperatorLayout.tsx    # Rewrite as AppShell wrapper
src/web/src/components/layout/AdminLayout.tsx       # Rewrite as AppShell wrapper
src/web/src/components/layout/WaitlistShellLayout.tsx # Rewrite as AppShell minimal wrapper
src/web/src/features/operator/pages/TeeSheet.tsx    # Redesign to use new components + PageTopbar
src/web/src/features/operator/__tests__/OperatorLayout.test.tsx  # Update if locators break
.claude/rules/frontend/react-conventions.md         # Replace "edit them freely" line with new convention
```

---

## Phase 1 — Tokens, Fonts, Conventions

### Task 1: Swap fonts in `index.html`

**Files:**
- Modify: `src/web/index.html`

- [ ] **Step 1: Replace the Albert Sans + Fraunces `<link>` with Plex Sans + Plex Mono + Libre Baskerville**

Open `src/web/index.html` and replace lines 9–12 (the existing `<link href="https://fonts.googleapis.com/css2?family=Albert+Sans...">`) with:

```html
<link
  href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:wght@300;400;500;600&family=IBM+Plex+Mono:wght@400;500&family=Libre+Baskerville:wght@400;700&display=swap"
  rel="stylesheet"
/>
```

The `<link rel="preconnect">` lines (7–8) stay unchanged.

- [ ] **Step 2: Verify the file**

Read the file back. Confirm it has Plex Sans + Plex Mono + Libre Baskerville and no Albert Sans / Fraunces references remain.

### Task 2: Update Fieldstone tokens and shadcn semantic mapping in `index.css`

**Files:**
- Modify: `src/web/src/index.css`

- [ ] **Step 1: Add Fieldstone palette variables to `:root` (replacing the OKLCH "warm professional palette")**

Replace the entire `:root` block (lines 52–109) with the following. Keep the surrounding `@theme inline` block (lines 7–50) and `.dark` block (lines 111–145) and `@layer base` block (lines 147–155) unchanged for now — they will be touched in subsequent steps.

```css
:root {
    --radius: 5px;

    /* Fonts */
    --font-heading: 'Libre Baskerville', Georgia, serif;
    --font-body: 'IBM Plex Sans', system-ui, sans-serif;
    --font-mono: 'IBM Plex Mono', ui-monospace, monospace;

    /* Fieldstone palette */
    --canvas: #f4f2ee;
    --paper: #faf9f7;
    --white: #ffffff;
    --ink: #1c1a18;
    --ink-secondary: #4a4742;
    --ink-muted: #8c8880;
    --ink-faint: #c8c4bc;
    --border-soft: #e0dcd4;
    --border-strong: #c8c4bc;

    --green: #2e6b42;
    --green-mid: #3d8a57;
    --green-light: #d4ead9;
    --green-faint: #edf6f0;

    --orange: #c45e1a;
    --orange-mid: #e07035;
    --orange-light: #f5dece;
    --orange-faint: #fdf3ec;

    --red: #c0392b;
    --red-light: #fce8e8;

    --blue: #2563a8;
    --blue-light: #ddeaf8;

    /* shadcn semantic mapping → Fieldstone */
    --background: var(--paper);
    --foreground: var(--ink);
    --card: var(--white);
    --card-foreground: var(--ink);
    --popover: var(--white);
    --popover-foreground: var(--ink);

    --primary: var(--green-faint);
    --primary-foreground: var(--green);

    --secondary: var(--canvas);
    --secondary-foreground: var(--ink-secondary);

    --muted: var(--canvas);
    --muted-foreground: var(--ink-muted);

    --accent: var(--canvas);
    --accent-foreground: var(--ink);

    --destructive: var(--red);

    --success: var(--green-mid);
    --success-foreground: var(--white);

    --border: var(--border-soft);
    --input: var(--border-soft);
    --ring: var(--green-mid);

    --chart-1: var(--green);
    --chart-2: var(--orange);
    --chart-3: var(--green-mid);
    --chart-4: var(--red);
    --chart-5: var(--ink-muted);

    /* Sidebar — DARK ink for Fieldstone */
    --sidebar: var(--ink);
    --sidebar-foreground: rgba(255, 255, 255, 0.7);
    --sidebar-primary: var(--green-mid);
    --sidebar-primary-foreground: var(--paper);
    --sidebar-accent: rgba(255, 255, 255, 0.08);
    --sidebar-accent-foreground: var(--paper);
    --sidebar-border: rgba(255, 255, 255, 0.08);
    --sidebar-ring: var(--green-mid);
}
```

- [ ] **Step 2: Update the `.dark` block to also use Fieldstone (kept identical to `:root` for now)**

Replace the entire `.dark` block (lines 111–145) with:

```css
.dark {
    /* Fieldstone has a single light theme. Dark mode is not supported in this redesign. */
    /* Keeping the .dark selector intact so existing class toggles don't break, */
    /* but values match :root so the app renders consistently. */
    --background: var(--paper);
    --foreground: var(--ink);
    --card: var(--white);
    --card-foreground: var(--ink);
    --popover: var(--white);
    --popover-foreground: var(--ink);
    --primary: var(--green-faint);
    --primary-foreground: var(--green);
    --secondary: var(--canvas);
    --secondary-foreground: var(--ink-secondary);
    --muted: var(--canvas);
    --muted-foreground: var(--ink-muted);
    --accent: var(--canvas);
    --accent-foreground: var(--ink);
    --destructive: var(--red);
    --success: var(--green-mid);
    --success-foreground: var(--white);
    --border: var(--border-soft);
    --input: var(--border-soft);
    --ring: var(--green-mid);
    --sidebar: var(--ink);
    --sidebar-foreground: rgba(255, 255, 255, 0.7);
    --sidebar-primary: var(--green-mid);
    --sidebar-primary-foreground: var(--paper);
    --sidebar-accent: rgba(255, 255, 255, 0.08);
    --sidebar-accent-foreground: var(--paper);
    --sidebar-border: rgba(255, 255, 255, 0.08);
    --sidebar-ring: var(--green-mid);
}
```

- [ ] **Step 3: Add `--font-mono` and the Fieldstone color tokens to the `@theme inline` block so Tailwind utilities can reach them**

Inside the `@theme inline` block (between lines 7–50), add the following lines just after the existing `--font-body: var(--font-body);`:

```css
    --font-mono: var(--font-mono);
    --color-canvas: var(--canvas);
    --color-paper: var(--paper);
    --color-ink: var(--ink);
    --color-ink-secondary: var(--ink-secondary);
    --color-ink-muted: var(--ink-muted);
    --color-ink-faint: var(--ink-faint);
    --color-border-strong: var(--border-strong);
    --color-green: var(--green);
    --color-green-mid: var(--green-mid);
    --color-green-light: var(--green-light);
    --color-green-faint: var(--green-faint);
    --color-orange: var(--orange);
    --color-orange-mid: var(--orange-mid);
    --color-orange-light: var(--orange-light);
    --color-orange-faint: var(--orange-faint);
    --color-red: var(--red);
    --color-red-light: var(--red-light);
    --color-blue: var(--blue);
    --color-blue-light: var(--blue-light);
```

This makes `bg-canvas`, `bg-paper`, `text-ink`, `border-border-strong`, `bg-green-faint`, `text-orange`, `font-mono`, etc. available as Tailwind utilities.

- [ ] **Step 4: Verify by running the dev server**

Run: `make dev`
Open `http://localhost:3000`. Confirm the app loads. The page will look different (warm paper background, dark sidebar, serif headings) but it should render. Click into the operator tee sheet, walkup waitlist, and one admin page to make sure nothing crashes.

Stop the dev server.

- [ ] **Step 5: Run the existing test suite**

Run: `pnpm --dir src/web test`
Expected: PASS. Tests assert on text content and roles, not visual styles, so they should be unaffected.

If tests fail, investigate before continuing — a token rename may have broken a test that relied on a class name.

- [ ] **Step 6: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 7: Commit**

```bash
git add src/web/index.html src/web/src/index.css
git commit -m "$(cat <<'EOF'
chore: introduce Fieldstone design tokens and fonts

Replaces the warm professional OKLCH palette with the Fieldstone hex
palette from the Shadowbrook mockup. Maps shadcn semantic tokens to
Fieldstone, including the sidebar tokens (now dark ink). Swaps fonts
to IBM Plex Sans + IBM Plex Mono + Libre Baskerville.

No source edits to shadcn primitives — the retheme happens entirely
through CSS variables.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

### Task 3: Update frontend conventions to mark shadcn primitives read-only

**Files:**
- Modify: `.claude/rules/frontend/react-conventions.md`

- [ ] **Step 1: Remove the "edit them freely" line and add the new theming convention**

In `.claude/rules/frontend/react-conventions.md`, find the line under `## Styling`:

```
- shadcn components are owned source files (not a node_module) — edit them freely
```

Replace it with:

```
- shadcn components are vendored source files but treated as read-only (see "Theming shadcn components" section)
```

Then add a new section after the existing `## Styling` section (and before `## Routing`):

```markdown
## Theming shadcn components

shadcn UI primitives in `src/web/src/components/ui/` are vendored but treated as **read-only**. Theme them by updating CSS variables in `src/web/src/index.css` (`--background`, `--primary`, `--border`, `--radius`, `--sidebar`, etc.) — never by editing variant classes in the component files.

When a design needs something the stock variants cannot express:

- **New visual variant of an existing primitive** (e.g. a "warn" button) → create a wrapper component in `components/ui/` that composes the primitive with extra classes. Do not add variants to the primitive itself.
- **New domain component** (e.g. `StatusBadge`, `StatusChip`) → new file in `components/ui/`, may compose shadcn primitives internally.

Why: keeping primitives stock means upstream shadcn updates apply with `pnpm dlx shadcn add --overwrite`, no merge conflicts. Forks accumulate drift; wrappers and tokens don't.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/rules/frontend/react-conventions.md
git commit -m "$(cat <<'EOF'
docs: mark shadcn primitives read-only in frontend conventions

Adopts the convention that shadcn UI primitives are vendored but
read-only. Theming happens via CSS variables in index.css; visual
variants and domain components live in wrappers, not edits to the
primitives themselves.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 2 — AppShell + Slot Mechanism

### Task 4: Create the slot mechanism context

**Files:**
- Create: `src/web/src/components/layout/AppShellContext.tsx`

- [ ] **Step 1: Write the context file**

Create `src/web/src/components/layout/AppShellContext.tsx`:

```tsx
import { createContext, useContext } from 'react';

/**
 * Portal targets that AppShell exposes to descendant pages.
 * Each ref points at a DOM node where the slot helpers will portal their content.
 * `null` means the slot is not currently mounted (e.g. minimal variant has no sidebar but always has topbar + content).
 */
export interface AppShellSlots {
  topbarLeft: HTMLDivElement | null;
  topbarMiddle: HTMLDivElement | null;
  topbarRight: HTMLDivElement | null;
  rightRail: HTMLDivElement | null;
}

const AppShellContext = createContext<AppShellSlots | null>(null);

export const AppShellProvider = AppShellContext.Provider;

export function useAppShellSlots(): AppShellSlots {
  const slots = useContext(AppShellContext);
  if (!slots) {
    throw new Error(
      'AppShell slot helpers (PageTopbar, PageRightRail) must be used inside <AppShell>'
    );
  }
  return slots;
}
```

### Task 5: Create `PageTopbar` slot helper

**Files:**
- Create: `src/web/src/components/layout/PageTopbar.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/components/layout/PageTopbar.tsx`:

```tsx
import { type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useAppShellSlots } from './AppShellContext';

export interface PageTopbarProps {
  left?: ReactNode;
  middle?: ReactNode;
  right?: ReactNode;
}

/**
 * Pages render <PageTopbar> from inside <Outlet> to populate the AppShell topbar.
 * Each prop portals into the corresponding region.
 * Slots not provided are rendered empty (the region exists but contains nothing).
 */
export function PageTopbar({ left, middle, right }: PageTopbarProps) {
  const slots = useAppShellSlots();

  return (
    <>
      {left && slots.topbarLeft && createPortal(left, slots.topbarLeft)}
      {middle && slots.topbarMiddle && createPortal(middle, slots.topbarMiddle)}
      {right && slots.topbarRight && createPortal(right, slots.topbarRight)}
    </>
  );
}
```

### Task 6: Create `PageRightRail` slot helper

**Files:**
- Create: `src/web/src/components/layout/PageRightRail.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/components/layout/PageRightRail.tsx`:

```tsx
import { type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useAppShellSlots } from './AppShellContext';

export interface PageRightRailProps {
  children: ReactNode;
}

/**
 * Pages render <PageRightRail>{content}</PageRightRail> to open the right rail.
 * To close it, conditionally render nothing.
 *
 * The right rail region in AppShell only mounts when at least one page is rendering this component.
 */
export function PageRightRail({ children }: PageRightRailProps) {
  const slots = useAppShellSlots();

  if (!slots.rightRail) return null;
  return createPortal(children, slots.rightRail);
}
```

### Task 7: Create the `AppShell` component

**Files:**
- Create: `src/web/src/components/layout/AppShell.tsx`

- [ ] **Step 1: Write the AppShell**

Create `src/web/src/components/layout/AppShell.tsx`:

```tsx
import { type ReactNode, useState } from 'react';
import { NavLink } from 'react-router';
import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
  SidebarFooter,
  SidebarMenu,
  SidebarMenuItem,
  SidebarMenuButton,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarProvider,
  SidebarInset,
  SidebarTrigger,
} from '@/components/ui/sidebar';
import UserMenu from '@/components/layout/UserMenu';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { AppShellProvider, type AppShellSlots } from './AppShellContext';

export interface NavItem {
  to: string;
  label: string;
  icon?: ReactNode;
  badge?: string | number;
}

export interface NavSection {
  label: string;
  items: NavItem[];
}

export interface NavConfig {
  sections: NavSection[];
}

export interface AppShellProps {
  variant: 'full' | 'minimal';
  navConfig?: NavConfig;
  /** Brand mark renderable — used in sidebar header (full) or topbar left (minimal). */
  brand: ReactNode;
  /** Optional handler for the user menu's "Switch course" item. */
  onSwitchCourse?: () => void;
  children: ReactNode;
}

/**
 * The unified shell. Two variants:
 * - "full": shadcn Sidebar + Topbar + content + optional RightRail (operator/admin pages)
 * - "minimal": Topbar + content + optional RightRail, no sidebar (WaitlistShellLayout)
 *
 * Pages contribute topbar and right rail content via <PageTopbar> and <PageRightRail>
 * helpers from inside <Outlet>.
 */
export function AppShell({ variant, navConfig, brand, onSwitchCourse, children }: AppShellProps) {
  const [topbarLeft, setTopbarLeft] = useState<HTMLDivElement | null>(null);
  const [topbarMiddle, setTopbarMiddle] = useState<HTMLDivElement | null>(null);
  const [topbarRight, setTopbarRight] = useState<HTMLDivElement | null>(null);
  const [rightRail, setRightRail] = useState<HTMLDivElement | null>(null);

  const slots: AppShellSlots = { topbarLeft, topbarMiddle, topbarRight, rightRail };

  if (variant === 'minimal') {
    return (
      <AppShellProvider value={slots}>
        <div className="flex min-h-screen flex-col bg-paper">
          <Topbar
            brand={brand}
            onSwitchCourse={onSwitchCourse}
            setLeft={setTopbarLeft}
            setMiddle={setTopbarMiddle}
            setRight={setTopbarRight}
            showSidebarTrigger={false}
          />
          <div className="flex flex-1 overflow-hidden">
            <main className="flex-1 overflow-auto">{children}</main>
            <RightRailRegion setRef={setRightRail} />
          </div>
        </div>
      </AppShellProvider>
    );
  }

  return (
    <AppShellProvider value={slots}>
      <SidebarProvider>
        {navConfig && <AppSidebar brand={brand} navConfig={navConfig} onSwitchCourse={onSwitchCourse} />}
        <SidebarInset className="bg-paper">
          <Topbar
            brand={null}
            onSwitchCourse={undefined}
            setLeft={setTopbarLeft}
            setMiddle={setTopbarMiddle}
            setRight={setTopbarRight}
            showSidebarTrigger={true}
          />
          <div className="flex flex-1 overflow-hidden">
            <main className="flex-1 overflow-auto">{children}</main>
            <RightRailRegion setRef={setRightRail} />
          </div>
        </SidebarInset>
      </SidebarProvider>
    </AppShellProvider>
  );
}

interface AppSidebarProps {
  brand: ReactNode;
  navConfig: NavConfig;
  onSwitchCourse?: () => void;
}

function AppSidebar({ brand, navConfig, onSwitchCourse }: AppSidebarProps) {
  const { user } = useAuth();
  const initials = (user?.displayName ?? user?.email ?? '?')
    .split(/\s+/)
    .map((p) => p[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();

  return (
    <Sidebar>
      <SidebarHeader>
        <div className="flex items-center gap-2 py-2 px-2">{brand}</div>
      </SidebarHeader>
      <SidebarContent>
        {navConfig.sections.map((section) => (
          <SidebarGroup key={section.label}>
            <SidebarGroupLabel>{section.label}</SidebarGroupLabel>
            <SidebarMenu>
              {section.items.map((item) => (
                <SidebarMenuItem key={item.to}>
                  <SidebarMenuButton asChild>
                    <NavLink to={item.to}>
                      {({ isActive }) => (
                        <span className={`flex w-full items-center gap-2 ${isActive ? 'font-semibold' : ''}`}>
                          {item.icon}
                          <span className="flex-1">{item.label}</span>
                          {item.badge != null && (
                            <span className="text-[10px] font-mono">{item.badge}</span>
                          )}
                        </span>
                      )}
                    </NavLink>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroup>
        ))}
      </SidebarContent>
      <SidebarFooter>
        <div className="flex items-center gap-2 px-2 py-2 text-sm">
          <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-green text-[10px] font-semibold text-white">
            {initials}
          </div>
          <div className="min-w-0 flex-1">
            <div className="truncate text-[12px] text-sidebar-foreground">
              {user?.displayName ?? user?.email}
            </div>
            <div className="truncate text-[10px] text-sidebar-foreground/60">{user?.role}</div>
          </div>
          <UserMenu onSwitchCourse={onSwitchCourse} />
        </div>
      </SidebarFooter>
    </Sidebar>
  );
}

interface TopbarProps {
  brand: ReactNode;
  onSwitchCourse?: () => void;
  setLeft: (el: HTMLDivElement | null) => void;
  setMiddle: (el: HTMLDivElement | null) => void;
  setRight: (el: HTMLDivElement | null) => void;
  showSidebarTrigger: boolean;
}

function Topbar({ brand, onSwitchCourse, setLeft, setMiddle, setRight, showSidebarTrigger }: TopbarProps) {
  return (
    <header className="flex h-14 shrink-0 items-center gap-5 border-b border-border bg-white px-6">
      {showSidebarTrigger && <SidebarTrigger className="md:hidden" />}
      {brand}
      <div ref={setLeft} className="flex items-center" />
      <div className="h-6 w-px bg-border" />
      <div ref={setMiddle} className="flex items-center gap-2" />
      <div ref={setRight} className="ml-auto flex items-center gap-2" />
      {brand && <UserMenu onSwitchCourse={onSwitchCourse} />}
    </header>
  );
}

function RightRailRegion({ setRef }: { setRef: (el: HTMLDivElement | null) => void }) {
  return (
    <aside
      ref={setRef}
      className="empty:hidden w-[272px] shrink-0 overflow-y-auto border-l border-border bg-white"
    />
  );
}
```

Note: the `empty:hidden` Tailwind selector means the right-rail aside collapses to display:none when no children are portaled into it, so the main content automatically takes full width. No state needed.

- [ ] **Step 2: Verify it compiles**

Run: `pnpm --dir src/web lint`
Expected: clean.

Then run: `pnpm --dir src/web exec tsc --noEmit`
Expected: no type errors. (If `exec tsc` doesn't work, fall back to `pnpm --dir src/web build` and stop after the type check passes.)

### Task 8: Commit AppShell + slot mechanism

- [ ] **Step 1: Commit**

```bash
git add src/web/src/components/layout/AppShell.tsx \
        src/web/src/components/layout/AppShellContext.tsx \
        src/web/src/components/layout/PageTopbar.tsx \
        src/web/src/components/layout/PageRightRail.tsx
git commit -m "$(cat <<'EOF'
feat(web): add AppShell with full + minimal variants and slot mechanism

Introduces a unified shell that composes shadcn's Sidebar primitive
plus a portal-based slot mechanism. Pages contribute topbar and
right-rail content from inside <Outlet> via <PageTopbar> and
<PageRightRail>. The right rail uses empty:hidden so the region
collapses automatically when no page is portaling content.

No existing layouts use this yet — that swap happens in the next commits.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 3 — Nav Configs and Layout Shims

### Task 9: Create operator and admin nav configs

**Files:**
- Create: `src/web/src/features/operator/navigation.ts`
- Create: `src/web/src/features/admin/navigation.ts`

- [ ] **Step 1: Write `features/operator/navigation.ts`**

Mirror the routes that exist in `OperatorLayout.tsx` today (Tee Sheet, Waitlist, Settings). Do not invent new routes.

```ts
import type { NavConfig } from '@/components/layout/AppShell';

export const operatorNav: NavConfig = {
  sections: [
    {
      label: 'Operations',
      items: [
        { to: '/operator/tee-sheet', label: 'Tee Sheet' },
        { to: '/operator/waitlist', label: 'Waitlist' },
      ],
    },
    {
      label: 'Management',
      items: [
        { to: '/operator/settings', label: 'Settings' },
      ],
    },
  ],
};
```

- [ ] **Step 2: Write `features/admin/navigation.ts`**

Read `src/web/src/features/admin/index.tsx` (or wherever admin routes are declared) to confirm the route paths. Then mirror them — only routes that exist today.

```ts
import type { NavConfig } from '@/components/layout/AppShell';

export const adminNav: NavConfig = {
  sections: [
    {
      label: 'Platform',
      items: [
        { to: '/admin', label: 'Dashboard' },
        { to: '/admin/orgs', label: 'Organizations' },
        { to: '/admin/courses', label: 'Courses' },
        { to: '/admin/users', label: 'Users' },
      ],
    },
    {
      label: 'System',
      items: [
        { to: '/admin/feature-flags', label: 'Feature Flags' },
        { to: '/admin/dead-letters', label: 'Dead Letters' },
      ],
    },
  ],
};
```

If any of those routes do not actually exist in the router, remove the corresponding item before continuing. The rule is: only items that resolve today.

### Task 10: Rewrite `OperatorLayout` as an `AppShell` wrapper

**Files:**
- Modify: `src/web/src/components/layout/OperatorLayout.tsx`

- [ ] **Step 1: Replace the file contents**

Replace the entire contents of `src/web/src/components/layout/OperatorLayout.tsx` with:

```tsx
import { Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
import { AppShell } from '@/components/layout/AppShell';
import { Badge } from '@/components/ui/badge';
import { ChevronsUpDown } from 'lucide-react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useOrgContext } from '@/features/operator/context/OrgContext';
import { operatorNav } from '@/features/operator/navigation';

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
          className="flex items-center gap-1 text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground hover:bg-sidebar-accent rounded-md px-1 -mx-1"
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

function OperatorBrand() {
  const { user } = useAuth();
  const isAdmin = user?.role === 'Admin';

  return (
    <>
      {isAdmin ? (
        <OrgSwitcher />
      ) : (
        <h1
          className="max-w-[180px] truncate text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground"
          title={user?.organization?.name ?? 'Teeforce'}
        >
          {user?.organization?.name ?? 'Teeforce'}
        </h1>
      )}
      <Badge variant={isAdmin ? 'default' : 'success'} className="text-[10px] px-1.5 py-0">
        {isAdmin ? 'Admin' : 'Operator'}
      </Badge>
    </>
  );
}

export default function OperatorLayout() {
  const { user } = useAuth();
  const { clearCourse } = useCourseContext();
  const navigate = useNavigate();

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <AppShell
      variant="full"
      navConfig={operatorNav}
      brand={<OperatorBrand />}
      onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined}
    >
      <Outlet />
    </AppShell>
  );
}
```

The `OrgSwitcher` and brand rendering logic is preserved from the original — only the chrome (sidebar/header) is now provided by `AppShell`.

### Task 11: Update `OperatorLayout` test if needed

**Files:**
- Modify: `src/web/src/features/operator/__tests__/OperatorLayout.test.tsx` (if assertions break)

- [ ] **Step 1: Run the existing test**

Run: `pnpm --dir src/web test src/web/src/features/operator/__tests__/OperatorLayout.test.tsx`

The three test cases assert:
1. `screen.getByText('Pine Valley Golf Club')` exists
2. `screen.getByText('Teeforce')` exists when user has no organization
3. The element with the long org name has class `truncate`, `max-w-[180px]`, and a `title` attribute

These should all still pass — `OperatorBrand` renders the org name with the same `truncate max-w-[180px]` class and `title` attribute. The `OrgContext` is used by `OrgSwitcher`, but the test mocks an Operator role (not Admin), so `OrgSwitcher` is not rendered.

- [ ] **Step 2: If the test fails because `useOrgContext` is not mocked**

`OperatorLayout` now imports `useOrgContext`, but the test only mocks `useAuth` and `useCourseContext`. Add a mock at the top of the test file:

```tsx
import { useOrgContext } from '@/features/operator/context/OrgContext';

vi.mock('@/features/operator/context/OrgContext');

const mockUseOrgContext = vi.mocked(useOrgContext);
```

And in `beforeEach`:

```tsx
mockUseOrgContext.mockReturnValue({
  org: null,
  selectOrg: vi.fn(),
  clearOrg: vi.fn(),
});
```

(Adjust the return-shape to match the actual `OrgContext` interface; read `src/web/src/features/operator/context/OrgContext.tsx` to confirm.)

Re-run the test. Expected: PASS.

- [ ] **Step 3: If any test still fails**

Investigate the actual failure. The brand rendering preserves `truncate`, `max-w-[180px]`, and the `title` attribute; the org name and `'Teeforce'` fallback are preserved. Any failure is likely a missing context mock — fix by mocking the context that's being read.

### Task 12: Rewrite `AdminLayout` as an `AppShell` wrapper

**Files:**
- Read first: `src/web/src/components/layout/AdminLayout.tsx` (to preserve its current chrome)
- Modify: `src/web/src/components/layout/AdminLayout.tsx`

- [ ] **Step 1: Read the existing file**

Read `src/web/src/components/layout/AdminLayout.tsx` end to end. Note any state, hooks, or per-page chrome logic so the rewrite preserves it.

- [ ] **Step 2: Write the replacement**

Replace the entire contents with:

```tsx
import { Outlet } from 'react-router';
import { AppShell } from '@/components/layout/AppShell';
import { adminNav } from '@/features/admin/navigation';

function AdminBrand() {
  return (
    <h1 className="text-lg font-semibold font-[family-name:var(--font-heading)] text-sidebar-foreground">
      Teeforce
    </h1>
  );
}

export default function AdminLayout() {
  return (
    <AppShell variant="full" navConfig={adminNav} brand={<AdminBrand />}>
      <Outlet />
    </AppShell>
  );
}
```

If the original `AdminLayout` rendered something other than just `<Outlet>` (e.g. role-specific guards, breadcrumbs), thread that logic through here. Do not silently drop functionality.

### Task 13: Rewrite `WaitlistShellLayout` as a minimal `AppShell` wrapper

**Files:**
- Modify: `src/web/src/components/layout/WaitlistShellLayout.tsx`

- [ ] **Step 1: Replace the file contents**

Replace the entire contents of `src/web/src/components/layout/WaitlistShellLayout.tsx` with:

```tsx
import { Outlet, useNavigate } from 'react-router';
import { useCallback } from 'react';
import { AppShell } from '@/components/layout/AppShell';
import { useCourseContext } from '@/features/operator/context/CourseContext';
import { useAuth } from '@/features/auth';

function WaitlistBrand() {
  const { course } = useCourseContext();
  const { user } = useAuth();
  const displayName = course?.name ?? user?.organization?.name ?? 'Teeforce';

  return (
    <span className="text-lg font-semibold font-[family-name:var(--font-heading)] text-ink">
      {displayName}
    </span>
  );
}

export default function WaitlistShellLayout() {
  const { clearCourse } = useCourseContext();
  const { user } = useAuth();
  const navigate = useNavigate();

  const handleSwitchCourse = useCallback(() => {
    clearCourse();
    navigate('/operator');
  }, [clearCourse, navigate]);

  const showSwitchCourse = (user?.courses?.length ?? 0) > 1;

  return (
    <AppShell
      variant="minimal"
      brand={<WaitlistBrand />}
      onSwitchCourse={showSwitchCourse ? handleSwitchCourse : undefined}
    >
      <Outlet />
    </AppShell>
  );
}
```

The functional behavior is identical: same `displayName` logic, same `handleSwitchCourse`, same `showSwitchCourse` condition, same `UserMenu` (now rendered by `AppShell`'s minimal Topbar).

### Task 14: Smoke and commit Phase 3

- [ ] **Step 1: Run the test suite**

Run: `pnpm --dir src/web test`
Expected: PASS. (The `OperatorLayout` test should be passing per Task 11.)

- [ ] **Step 2: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 3: Manual smoke**

Run: `make dev`. Open the browser and:
- Visit `/operator/tee-sheet` — confirm sidebar renders, nav items present, no console errors
- Visit `/operator/waitlist` — confirm same chrome
- Visit an `/admin/*` route — confirm admin sidebar renders
- Visit a route that uses `WaitlistShellLayout` (check `app/router.tsx` for which route — likely `/operator/waitlist` if it's the phase-1 entry, or a course-scoped subpath)

If anything fails to render, stop and fix before continuing. Stop the dev server.

- [ ] **Step 4: Commit**

```bash
git add src/web/src/features/operator/navigation.ts \
        src/web/src/features/admin/navigation.ts \
        src/web/src/components/layout/OperatorLayout.tsx \
        src/web/src/components/layout/AdminLayout.tsx \
        src/web/src/components/layout/WaitlistShellLayout.tsx \
        src/web/src/features/operator/__tests__/OperatorLayout.test.tsx
git commit -m "$(cat <<'EOF'
refactor(web): rewrite layouts as AppShell wrappers

OperatorLayout, AdminLayout, and WaitlistShellLayout become thin
wrappers around the unified AppShell. operatorNav and adminNav configs
are colocated with their feature folders. Routing is unchanged.

OperatorLayout preserves the OrgSwitcher dropdown for admin users
and the static org name for operators. WaitlistShellLayout preserves
displayName fallback logic and showSwitchCourse handling.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 4 — New Primitives

### Task 15: Create `StatusBadge`

**Files:**
- Create: `src/web/src/components/ui/status-badge.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/components/ui/status-badge.tsx`:

```tsx
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

export type StatusBadgeStatus = 'booked' | 'open' | 'waitlist' | 'checkedin' | 'noshowed';

const STATUS_STYLES: Record<StatusBadgeStatus, { className: string; label: string }> = {
  booked:    { className: 'bg-green-faint text-green border-green-light',     label: 'Booked' },
  open:      { className: 'bg-canvas text-ink-muted border-border',           label: 'Open' },
  waitlist:  { className: 'bg-orange-faint text-orange border-orange-light',  label: 'Waitlist' },
  checkedin: { className: 'bg-blue-light text-blue border-blue-light',        label: 'Checked in' },
  noshowed:  { className: 'bg-red-light text-red border-red-light',           label: 'No show' },
};

export interface StatusBadgeProps {
  status: StatusBadgeStatus;
  /** Override the default label text. */
  children?: React.ReactNode;
}

export function StatusBadge({ status, children }: StatusBadgeProps) {
  const { className, label } = STATUS_STYLES[status];
  return (
    <Badge variant="outline" className={cn('rounded-[4px] px-2 py-[3px] text-[10px] font-medium border', className)}>
      {children ?? label}
    </Badge>
  );
}
```

### Task 16: Create `StatusChip`

**Files:**
- Create: `src/web/src/components/ui/status-chip.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/components/ui/status-chip.tsx`:

```tsx
import { cn } from '@/lib/utils';

export type StatusChipTone = 'green' | 'orange' | 'gray';

const TONE_STYLES: Record<StatusChipTone, { container: string; dot: string }> = {
  green:  { container: 'bg-green-faint text-green border-green-light',     dot: 'bg-green-mid' },
  orange: { container: 'bg-orange-faint text-orange border-orange-light',  dot: 'bg-orange-mid' },
  gray:   { container: 'bg-canvas text-ink-secondary border-border',       dot: 'bg-ink-faint' },
};

export interface StatusChipProps {
  tone: StatusChipTone;
  children: React.ReactNode;
}

export function StatusChip({ tone, children }: StatusChipProps) {
  const { container, dot } = TONE_STYLES[tone];
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium',
        container,
      )}
    >
      <span className={cn('h-1.5 w-1.5 shrink-0 rounded-full', dot)} />
      {children}
    </span>
  );
}
```

### Task 17: Create `PanelSection`

**Files:**
- Create: `src/web/src/components/layout/PanelSection.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/components/layout/PanelSection.tsx`:

```tsx
import type { ReactNode } from 'react';

export interface PanelSectionProps {
  title: string;
  link?: { label: string; href: string };
  children: ReactNode;
}

export function PanelSection({ title, link, children }: PanelSectionProps) {
  return (
    <section className="border-b border-border p-4">
      <header className="mb-3 flex items-center justify-between">
        <h3 className="text-[11px] font-medium uppercase tracking-[0.1em] text-ink-muted">
          {title}
        </h3>
        {link && (
          <a href={link.href} className="text-[11px] text-green hover:underline">
            {link.label}
          </a>
        )}
      </header>
      <div>{children}</div>
    </section>
  );
}
```

### Task 18: Commit Phase 4

- [ ] **Step 1: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

- [ ] **Step 2: Commit**

```bash
git add src/web/src/components/ui/status-badge.tsx \
        src/web/src/components/ui/status-chip.tsx \
        src/web/src/components/layout/PanelSection.tsx
git commit -m "$(cat <<'EOF'
feat(web): add StatusBadge, StatusChip, and PanelSection primitives

Three small Fieldstone primitives. StatusBadge wraps shadcn Badge with
domain status variants (booked/open/waitlist/checkedin/noshowed) —
variants for statuses we don't currently produce are defined for use
by future cluster pages. StatusChip is a new dot-pill primitive for
topbar counters. PanelSection wraps right-rail sections with the
mockup's uppercase title style.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Phase 5 — TeeSheet Redesign

### Task 19: Create tee sheet helpers

**Files:**
- Create: `src/web/src/features/operator/components/teeSheetHelpers.ts`

- [ ] **Step 1: Write the helpers**

Create `src/web/src/features/operator/components/teeSheetHelpers.ts`:

```ts
import type { StatusBadgeStatus } from '@/components/ui/status-badge';

/**
 * Maps the tee sheet API's slot.status enum to the visual StatusBadge variants.
 * The API currently only emits 'booked' and 'open'; other variants are defined
 * in StatusBadge for future use but unreachable from this mapper today.
 */
export function mapTeeTimeStatus(status: string): StatusBadgeStatus {
  switch (status) {
    case 'booked':
      return 'booked';
    case 'open':
    default:
      return 'open';
  }
}

/**
 * Derives initials from a name. Returns up to two uppercase characters.
 * Returns '?' if the name is empty.
 */
export function getInitials(name: string | null | undefined): string {
  if (!name || !name.trim()) return '?';
  return name
    .trim()
    .split(/\s+/)
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join('')
    .toUpperCase();
}
```

### Task 20: Create `PlayerAvatar`

**Files:**
- Create: `src/web/src/features/operator/components/PlayerAvatar.tsx`

- [ ] **Step 1: Write the component**

Create `src/web/src/features/operator/components/PlayerAvatar.tsx`:

```tsx
import { cn } from '@/lib/utils';
import { getInitials } from './teeSheetHelpers';

export type PlayerAvatarTone = 'green' | 'orange' | 'gray';

const TONE_STYLES: Record<PlayerAvatarTone, string> = {
  green:  'bg-green-light text-green',
  orange: 'bg-orange-light text-orange',
  gray:   'bg-border-strong text-ink-muted',
};

export interface PlayerAvatarProps {
  name: string | null | undefined;
  tone?: PlayerAvatarTone;
}

export function PlayerAvatar({ name, tone = 'green' }: PlayerAvatarProps) {
  return (
    <div
      className={cn(
        'flex h-6 w-6 shrink-0 items-center justify-center rounded-[4px] text-[9px] font-semibold',
        TONE_STYLES[tone],
      )}
    >
      {getInitials(name)}
    </div>
  );
}
```

### Task 21: Create `EmptySlot` and `NowMarker`

**Files:**
- Create: `src/web/src/features/operator/components/EmptySlot.tsx`
- Create: `src/web/src/features/operator/components/NowMarker.tsx`

- [ ] **Step 1: Write `EmptySlot.tsx`**

```tsx
export function EmptySlot() {
  return <span className="text-[11px] italic text-ink-faint">—</span>;
}
```

- [ ] **Step 2: Write `NowMarker.tsx`**

```tsx
import { formatWallClockTime } from '@/lib/course-time';

export interface NowMarkerProps {
  /** Wall-clock ISO string for "now" in the course's timezone. */
  now: string;
}

export function NowMarker({ now }: NowMarkerProps) {
  return (
    <div className="pointer-events-none flex items-center gap-3 px-6 py-1">
      <div className="h-px flex-1 bg-green-mid/30" />
      <span className="font-mono text-[9px] uppercase tracking-[0.08em] text-green">
        ▶ Now · {formatWallClockTime(now)}
      </span>
      <div className="h-px flex-1 bg-green-mid/30" />
    </div>
  );
}
```

### Task 22: Create `PlayerCell`

**Files:**
- Create: `src/web/src/features/operator/components/PlayerCell.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { PlayerAvatar } from './PlayerAvatar';
import { EmptySlot } from './EmptySlot';

export interface PlayerCellProps {
  golferName: string | null | undefined;
  isPast?: boolean;
}

export function PlayerCell({ golferName, isPast }: PlayerCellProps) {
  if (!golferName) {
    return (
      <div className="flex items-center">
        <EmptySlot />
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2">
      <PlayerAvatar name={golferName} tone={isPast ? 'gray' : 'green'} />
      <span className={`text-[12px] ${isPast ? 'text-ink-muted' : 'text-ink'}`}>
        {golferName}
      </span>
    </div>
  );
}
```

### Task 23: Create `TeeSheetTopbarTitle`

**Files:**
- Create: `src/web/src/features/operator/components/TeeSheetTopbarTitle.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { formatWallClockDate } from '@/lib/course-time';

export interface TeeSheetTopbarTitleProps {
  courseName: string;
  /** ISO date string (yyyy-mm-dd) of the currently selected day. */
  selectedDate: string;
  /** Optional: a tee time ISO string used to render the formatted date. Falls back to selectedDate. */
  anchorTeeTime?: string;
}

export function TeeSheetTopbarTitle({ courseName, selectedDate, anchorTeeTime }: TeeSheetTopbarTitleProps) {
  const formatted = anchorTeeTime ? formatWallClockDate(anchorTeeTime) : selectedDate;
  return (
    <div className="flex flex-col">
      <span className="text-[15px] font-semibold leading-tight text-ink">{courseName}</span>
      <span className="text-[12px] leading-tight text-ink-muted">{formatted}</span>
    </div>
  );
}
```

### Task 24: Create `TeeSheetDateNav`

**Files:**
- Create: `src/web/src/features/operator/components/TeeSheetDateNav.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { Button } from '@/components/ui/button';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';

export interface TeeSheetDateNavProps {
  selectedDate: string;
  onDateChange: (next: string) => void;
  courseTimeZoneId: string | undefined;
}

function addDays(dateStr: string, delta: number): string {
  const [y, m, d] = dateStr.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1, d));
  dt.setUTCDate(dt.getUTCDate() + delta);
  return dt.toISOString().slice(0, 10);
}

export function TeeSheetDateNav({ selectedDate, onDateChange, courseTimeZoneId }: TeeSheetDateNavProps) {
  const today = getCourseToday(courseTimeZoneId ?? getBrowserTimeZone());

  return (
    <div className="flex items-center gap-1.5">
      <Button
        variant="outline"
        size="sm"
        onClick={() => onDateChange(addDays(selectedDate, -1))}
        aria-label="Previous day"
      >
        <ChevronLeft className="h-3.5 w-3.5" />
      </Button>
      <Button
        variant={selectedDate === today ? 'default' : 'outline'}
        size="sm"
        onClick={() => onDateChange(today)}
      >
        Today
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={() => onDateChange(addDays(selectedDate, 1))}
        aria-label="Next day"
      >
        <ChevronRight className="h-3.5 w-3.5" />
      </Button>
      <input
        type="date"
        value={selectedDate}
        onChange={(e) => onDateChange(e.target.value)}
        className="ml-1 h-8 rounded-[5px] border border-border bg-white px-2 text-[11px] text-ink"
        aria-label="Pick date"
      />
    </div>
  );
}
```

### Task 25: Create `TeeSheetRow`

**Files:**
- Create: `src/web/src/features/operator/components/TeeSheetRow.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { formatWallClockTime } from '@/lib/course-time';
import { StatusBadge } from '@/components/ui/status-badge';
import { PlayerCell } from './PlayerCell';
import { mapTeeTimeStatus } from './teeSheetHelpers';

export type TeeSheetRowVariant = 'past' | 'current' | 'default';

export interface TeeSheetRowSlot {
  teeTime: string;
  status: string;
  golferName: string | null | undefined;
  playerCount: number | null | undefined;
}

export interface TeeSheetRowProps {
  slot: TeeSheetRowSlot;
  variant: TeeSheetRowVariant;
}

const VARIANT_STYLES: Record<TeeSheetRowVariant, string> = {
  past:    'bg-canvas',
  current: 'bg-white shadow-[inset_3px_0_0_var(--green)]',
  default: 'bg-paper',
};

export function TeeSheetRow({ slot, variant }: TeeSheetRowProps) {
  const isPast = variant === 'past';
  const isOpen = slot.status !== 'booked';

  return (
    <div
      className={`grid min-h-[54px] grid-cols-[100px_120px_1fr_80px] items-center gap-4 border-b border-border px-6 transition-colors hover:bg-white ${VARIANT_STYLES[variant]}`}
    >
      <div className={`font-mono text-[12px] ${isPast ? 'text-ink-muted' : 'text-ink'}`}>
        {formatWallClockTime(slot.teeTime)}
      </div>
      <div>
        <StatusBadge status={mapTeeTimeStatus(slot.status)} />
      </div>
      <PlayerCell golferName={slot.golferName} isPast={isPast} />
      <div className={`font-mono text-[12px] text-right ${isPast ? 'text-ink-muted' : 'text-ink'}`}>
        {isOpen ? '—' : (slot.playerCount ?? '—')}
      </div>
    </div>
  );
}
```

### Task 26: Create `TeeSheetGrid`

**Files:**
- Create: `src/web/src/features/operator/components/TeeSheetGrid.tsx`

- [ ] **Step 1: Write the component**

```tsx
import { Fragment } from 'react';
import { TeeSheetRow, type TeeSheetRowSlot, type TeeSheetRowVariant } from './TeeSheetRow';
import { NowMarker } from './NowMarker';

export interface TeeSheetGridProps {
  slots: TeeSheetRowSlot[];
  /** Wall-clock ISO string for "now" in the course's timezone. */
  now: string;
}

function variantFor(slot: TeeSheetRowSlot, now: string, isFirstFuture: boolean): TeeSheetRowVariant {
  if (slot.teeTime < now) return 'past';
  if (isFirstFuture) return 'current';
  return 'default';
}

export function TeeSheetGrid({ slots, now }: TeeSheetGridProps) {
  // Identify the index of the first slot whose teeTime is >= now (the "current" row).
  // The NowMarker is rendered immediately before that row.
  const firstFutureIdx = slots.findIndex((s) => s.teeTime >= now);

  return (
    <div className="bg-paper">
      <div className="sticky top-0 z-10 grid grid-cols-[100px_120px_1fr_80px] gap-4 border-b border-border bg-white px-6 py-2.5">
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted">Time</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted">Status</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted">Golfer</div>
        <div className="text-[10px] font-medium uppercase tracking-[0.1em] text-ink-muted text-right">Players</div>
      </div>
      {slots.map((slot, i) => (
        <Fragment key={`${slot.teeTime}-${i}`}>
          {i === firstFutureIdx && <NowMarker now={now} />}
          <TeeSheetRow slot={slot} variant={variantFor(slot, now, i === firstFutureIdx)} />
        </Fragment>
      ))}
      {firstFutureIdx === -1 && <NowMarker now={now} />}
    </div>
  );
}
```

### Task 27: Refactor the `TeeSheet` page to use the new components

**Files:**
- Modify: `src/web/src/features/operator/pages/TeeSheet.tsx`

- [ ] **Step 1: Replace the file contents**

Replace the entire contents of `src/web/src/features/operator/pages/TeeSheet.tsx` with:

```tsx
import { useState } from 'react';
import { Link } from 'react-router';
import { useTeeSheet } from '@/features/operator/hooks/useTeeSheet';
import { useCourseContext } from '../context/CourseContext';
import { getCourseToday, getBrowserTimeZone } from '@/lib/course-time';
import { Button } from '@/components/ui/button';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { TeeSheetTopbarTitle } from '../components/TeeSheetTopbarTitle';
import { TeeSheetDateNav } from '../components/TeeSheetDateNav';
import { TeeSheetGrid } from '../components/TeeSheetGrid';

export default function TeeSheet() {
  const { course } = useCourseContext();
  const timeZone = course?.timeZoneId ?? getBrowserTimeZone();
  const [selectedDate, setSelectedDate] = useState<string>(() => getCourseToday(timeZone));
  const teeSheetQuery = useTeeSheet(course?.id, selectedDate);

  if (!course) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <p className="text-muted-foreground">
          Select a course from the sidebar to view the tee sheet.
        </p>
      </div>
    );
  }

  const data = teeSheetQuery.data;
  const anchorTeeTime = data && data.slots.length > 0 ? data.slots[0]!.teeTime : undefined;
  const now = new Date().toISOString();

  return (
    <>
      <PageTopbar
        left={
          <TeeSheetTopbarTitle
            courseName={data?.courseName ?? course.name}
            selectedDate={selectedDate}
            anchorTeeTime={anchorTeeTime}
          />
        }
        right={
          <TeeSheetDateNav
            selectedDate={selectedDate}
            onDateChange={setSelectedDate}
            courseTimeZoneId={course.timeZoneId}
          />
        }
      />

      {teeSheetQuery.isError && (() => {
        const message = teeSheetQuery.error instanceof Error
          ? teeSheetQuery.error.message
          : 'Failed to load tee sheet';
        const isNotConfigured = message.toLowerCase().includes('not configured');
        return isNotConfigured ? (
          <div className="m-6 max-w-md rounded-md border border-border bg-white p-6 text-center">
            <p className="font-medium text-ink">Configure your tee times to get started</p>
            <p className="mt-1 text-sm text-ink-muted">
              Set your tee time interval, first tee time, and last tee time in Settings.
            </p>
            <Button asChild variant="default" size="sm" className="mt-4">
              <Link to="/operator/settings">Go to Settings</Link>
            </Button>
          </div>
        ) : (
          <p className="m-6 text-sm text-destructive">{message}</p>
        );
      })()}

      {data && <TeeSheetGrid slots={data.slots} now={now} />}
    </>
  );
}
```

Notable behavior preserved:
- `if (!course)` "Select a course" guard — same text, restyled to use new tokens
- `not configured` error → "Configure your tee times" CTA → "Go to Settings" button — preserved exactly
- Generic error → red text — preserved
- `useTeeSheet` query — unchanged
- No new endpoints, no new fields

Behavior dropped:
- `<PageHeader title="Tee Sheet">` — replaced by `<PageTopbar>`
- The standalone `<Input type="date">` block — replaced by `TeeSheetDateNav`
- The `<h2>` `courseName - date` heading — moved into `TeeSheetTopbarTitle`
- `<div className="p-6">` outer wrapper — `AppShell`'s content region handles layout

### Task 28: Run tests and fix locator breakage

- [ ] **Step 1: Run the existing tests**

Run: `pnpm --dir src/web test`

Watch for failures in tests that touch the tee sheet (any file matching `tee-sheet`, `TeeSheet`, etc.). The most likely failure modes are:
- Tests that asserted `getByLabelText('Date')` or `getByRole('textbox', { name: /date/i })` — the date input now lives inside `TeeSheetDateNav` and is still labeled, so this should still work but the surrounding context differs
- Tests that asserted on `<h2>` text — the `<h2>` is gone

- [ ] **Step 2: For each failing test, update the locator (not the assertion)**

If a test asserts that the tee sheet shows a slot's time / status / golfer name, those assertions stay. Only update *how* the test finds the element. Examples:

- If a test does `screen.getByRole('heading', { name: /tee sheet/i })`, replace with `screen.getByText(course.name)` (the course name now appears in the topbar title) or remove the assertion if the heading no longer exists meaningfully.
- If a test does `screen.getByLabelText('Date')`, the date input is still labeled `Pick date` — update to `getByLabelText('Pick date')`.

If you find an assertion you cannot preserve because the underlying element is gone for a good reason, post a brief justification comment on the PR explaining the change. Do not silently delete tests.

- [ ] **Step 3: Re-run tests until green**

Run: `pnpm --dir src/web test`
Expected: PASS.

- [ ] **Step 4: Run lint**

Run: `pnpm --dir src/web lint`
Expected: clean.

### Task 29: Manual smoke

- [ ] **Step 1: Run the dev server**

Run: `make dev`

- [ ] **Step 2: Click through the redesigned tee sheet**

Open `http://localhost:3000/operator/tee-sheet`. Verify:
- Sidebar renders dark with the new nav structure
- Topbar shows course name + date on the left, date nav (`‹ Today ›` + date input) on the right
- Grid renders with the four columns (Time / Status / Golfer / Players)
- "Now" marker appears between past and future rows
- Past rows have canvas background; current row has white + green left bar
- Status badges render in the new style
- Date nav buttons step ±1 day and Today returns to course-local today
- The "not configured" error state still renders with the Go-to-Settings CTA (test by switching to a course with no tee time settings if available, or temporarily simulate the error)

- [ ] **Step 3: Click through other operator/admin pages**

Visit `/operator/waitlist`, one `/admin/*` page, and one walkup waitlist route. Verify they render without console errors. They will look different (token cascade) but should not be broken.

- [ ] **Step 4: Click through one golfer-facing page**

Visit a `/golfer/*` or walk-up route. Verify it still renders. If anything looks badly broken (not just visually different), note the file and add a targeted override after this task — do not redesign the golfer page in this PR.

- [ ] **Step 5: Stop the dev server**

### Task 30: Final commit

- [ ] **Step 1: Stage and commit the tee sheet redesign**

```bash
git add src/web/src/features/operator/components/teeSheetHelpers.ts \
        src/web/src/features/operator/components/PlayerAvatar.tsx \
        src/web/src/features/operator/components/EmptySlot.tsx \
        src/web/src/features/operator/components/NowMarker.tsx \
        src/web/src/features/operator/components/PlayerCell.tsx \
        src/web/src/features/operator/components/TeeSheetTopbarTitle.tsx \
        src/web/src/features/operator/components/TeeSheetDateNav.tsx \
        src/web/src/features/operator/components/TeeSheetRow.tsx \
        src/web/src/features/operator/components/TeeSheetGrid.tsx \
        src/web/src/features/operator/pages/TeeSheet.tsx
git commit -m "$(cat <<'EOF'
feat(operator): redesign tee sheet in Fieldstone language

The tee sheet is the proof page for the new design language. Replaces
the PageHeader + Input + Table layout with a topbar (course name +
date nav) and a Fieldstone grid (mono time cells, restyled status
badges, now marker, current/past row treatments).

No new functionality. Same data shape from useTeeSheet, same loading /
error / empty states preserved exactly. The "Configure your tee times"
CTA still points to settings.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 2: Final verification — full test suite, lint, build**

```bash
pnpm --dir src/web lint
pnpm --dir src/web test
pnpm --dir src/web build
```

All three should pass. If `build` reveals type errors not caught by the IDE, fix them and amend or add a fix-up commit.

- [ ] **Step 3: Backend build (untouched, but project rule)**

```bash
dotnet build teeforce.slnx
```

Expected: PASS. (This plan does not modify backend code, but the project convention says to verify after making changes; this catches any accidental import drift.)

- [ ] **Step 4: Final make dev smoke**

```bash
make dev
```

One more click-through of the tee sheet, walkup waitlist, an admin page, and a golfer page to confirm everything still works after all commits land. Stop the dev server.

---

## Done criteria checklist (mirrors spec section 6)

- [ ] Tokens defined in `index.css`, mapped to shadcn semantic tokens including `--sidebar` family
- [ ] Fonts loaded via `index.html` with preconnect
- [ ] `--font-heading`, `--font-body`, `--font-mono` resolve to Libre Baskerville / Plex Sans / Plex Mono
- [ ] `@theme inline` block exposes Fieldstone colors as Tailwind utilities
- [ ] `AppShell` implemented with both `full` and `minimal` variants, slot mechanism via portals
- [ ] `OperatorLayout`, `AdminLayout`, `WaitlistShellLayout` rewritten as wrappers
- [ ] `operatorNav` colocated in `features/operator/navigation.ts`
- [ ] `adminNav` colocated in `features/admin/navigation.ts`
- [ ] `StatusBadge`, `StatusChip`, `PanelSection` exist
- [ ] `PageHeader` displays in Libre Baskerville (verified visually — no source change needed)
- [ ] `TeeSheet` page redesigned: PageTopbar with course name + date nav, mono time cells, restyled status badges, now marker, current/past row treatments, all original data and behavior preserved
- [ ] `react-conventions.md` updated: "edit them freely" line removed, theming convention added
- [ ] All existing unit tests passing (with locator updates where forced)
- [ ] `pnpm --dir src/web lint` clean
- [ ] `pnpm --dir src/web test` clean
- [ ] `pnpm --dir src/web build` clean
- [ ] `dotnet build teeforce.slnx` clean
- [ ] Manual smoke via `make dev`: tee sheet, walkup waitlist, one admin page, one golfer page render without crashes
- [ ] PR description includes before/after screenshots: tee sheet, walkup waitlist, one admin list page, one golfer page
- [ ] Cluster 1–4 follow-up issues filed under an "Operator/Admin redesign rollout" parent epic and linked from the PR description (file these via `gh issue create` after the PR is open)
