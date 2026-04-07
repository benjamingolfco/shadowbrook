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
