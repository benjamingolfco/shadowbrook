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
