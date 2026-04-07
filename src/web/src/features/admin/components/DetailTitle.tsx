import { Link } from 'react-router';
import { ChevronLeft } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';

export function DetailTitle({ backTo, title }: { backTo: string; title?: string }) {
  return (
    <div className="flex items-center gap-3">
      <Link
        to={backTo}
        className="text-ink-muted hover:text-ink"
        aria-label="Back"
      >
        <ChevronLeft className="h-4 w-4" />
      </Link>
      <h1 className="font-[family-name:var(--font-heading)] text-[18px] text-ink">
        {title ?? <Skeleton className="h-5 w-40 inline-block" />}
      </h1>
    </div>
  );
}
