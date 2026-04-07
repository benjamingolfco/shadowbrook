import { render, type RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router';
import { useEffect, useState, type ReactElement, type ReactNode } from 'react';
import { AppShellProvider, type AppShellSlots } from '@/components/layout/AppShellContext';

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

/**
 * Test-only AppShell stub: provides real DOM nodes for the topbar slots so
 * pages that render `<PageTopbar>` during tests don't throw from
 * `useAppShellSlots()`. The slot divs are appended to the document body so
 * React Testing Library's `screen` queries can still find portaled content.
 */
function TestAppShellProvider({ children }: { children: ReactNode }) {
  const [slots] = useState<AppShellSlots>(() => {
    const topbarLeft = document.createElement('div');
    const topbarMiddle = document.createElement('div');
    const topbarRight = document.createElement('div');
    const rightRail = document.createElement('div');
    document.body.appendChild(topbarLeft);
    document.body.appendChild(topbarMiddle);
    document.body.appendChild(topbarRight);
    document.body.appendChild(rightRail);
    return { topbarLeft, topbarMiddle, topbarRight, rightRail };
  });

  useEffect(() => {
    return () => {
      slots.topbarLeft?.remove();
      slots.topbarMiddle?.remove();
      slots.topbarRight?.remove();
      slots.rightRail?.remove();
    };
  }, [slots]);

  return <AppShellProvider value={slots}>{children}</AppShellProvider>;
}

interface WrapperProps {
  children: ReactNode;
  route?: string;
}

function AllProviders({ children, route }: WrapperProps) {
  const queryClient = createTestQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={route ? [route] : undefined}>
        <TestAppShellProvider>{children}</TestAppShellProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

interface CustomRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  route?: string;
}

function customRender(ui: ReactElement, options?: CustomRenderOptions) {
  const { route, ...renderOptions } = options ?? {};
  return render(ui, {
    wrapper: ({ children }) => <AllProviders route={route}>{children}</AllProviders>,
    ...renderOptions,
  });
}

// re-export everything
export * from '@testing-library/react';
export { customRender as render };
