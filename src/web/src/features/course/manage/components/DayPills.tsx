import { cn } from '@/lib/utils';

const DAYS = [
  { value: 1, label: 'Mon' },
  { value: 2, label: 'Tue' },
  { value: 3, label: 'Wed' },
  { value: 4, label: 'Thu' },
  { value: 5, label: 'Fri' },
  { value: 6, label: 'Sat' },
  { value: 0, label: 'Sun' },
] as const;

interface DayPillsProps {
  value: number[];
  onChange: (days: number[]) => void;
  disabled?: boolean;
}

export function DayPills({ value, onChange, disabled }: DayPillsProps) {
  function toggle(day: number) {
    if (disabled) return;
    if (value.includes(day)) {
      onChange(value.filter((d) => d !== day));
    } else {
      onChange([...value, day]);
    }
  }

  return (
    <div className="flex gap-1.5">
      {DAYS.map((day) => {
        const selected = value.includes(day.value);
        return (
          <button
            key={day.value}
            type="button"
            disabled={disabled}
            onClick={() => toggle(day.value)}
            className={cn(
              'rounded-md px-2.5 py-1 text-xs font-medium transition-colors',
              selected
                ? 'bg-primary text-primary-foreground'
                : 'border border-border text-muted-foreground hover:bg-accent',
              disabled && 'opacity-50 cursor-not-allowed',
            )}
          >
            {day.label}
          </button>
        );
      })}
    </div>
  );
}

interface DayPillsReadonlyProps {
  days: number[];
}

export function DayPillsReadonly({ days }: DayPillsReadonlyProps) {
  return (
    <div className="flex gap-1">
      {DAYS.filter((d) => days.includes(d.value)).map((day) => (
        <span
          key={day.value}
          className="rounded-md bg-secondary px-2 py-0.5 text-xs font-medium text-secondary-foreground"
        >
          {day.label}
        </span>
      ))}
    </div>
  );
}
