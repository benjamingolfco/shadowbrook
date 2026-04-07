import { type ReactNode, useState } from 'react';
import { NavLink } from 'react-router';
import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
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
        <div className="flex min-h-screen flex-col bg-background">
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
        {navConfig && <AppSidebar brand={brand} navConfig={navConfig} />}
        <SidebarInset className="bg-background">
          <Topbar
            brand={null}
            onSwitchCourse={onSwitchCourse}
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
}

function AppSidebar({ brand, navConfig }: AppSidebarProps) {
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
    <header className="grid h-14 shrink-0 grid-cols-[1fr_auto_1fr] items-center gap-5 border-b border-border bg-white px-6">
      <div className="flex items-center gap-5 justify-self-start">
        {showSidebarTrigger && <SidebarTrigger className="md:hidden" />}
        {brand}
        <div ref={setLeft} className="flex items-center empty:hidden" />
      </div>
      <div ref={setMiddle} className="flex items-center gap-2 justify-self-center" />
      <div className="flex items-center gap-3 justify-self-end">
        <div ref={setRight} className="flex items-center gap-2 empty:hidden" />
        <UserMenu onSwitchCourse={onSwitchCourse} />
      </div>
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
