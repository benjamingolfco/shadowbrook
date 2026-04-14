import { useState } from 'react';
import { MoreHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { DayPillsReadonly } from './DayPills';
import { cn } from '@/lib/utils';
import type { RateSchedule } from '@/types/course';

function formatTime(time: string): string {
  const parts = time.split(':').map(Number);
  const hours = parts[0] ?? 0;
  const minutes = parts[1] ?? 0;
  const period = hours >= 12 ? 'PM' : 'AM';
  const displayHour = hours % 12 || 12;
  return `${displayHour}:${minutes.toString().padStart(2, '0')} ${period}`;
}

interface RateScheduleListProps {
  schedules: RateSchedule[];
  onAdd: () => void;
  onEdit: (schedule: RateSchedule) => void;
  onDelete: (scheduleId: string) => void;
  isDeleting: boolean;
}

export function RateScheduleList({ schedules, onAdd, onEdit, onDelete, isDeleting }: RateScheduleListProps) {
  const [deleteTarget, setDeleteTarget] = useState<RateSchedule | null>(null);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          Rate Schedules
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {schedules.length === 0 ? (
          <div className="text-center py-6">
            <p className="text-sm text-muted-foreground mb-4">
              No rate schedules yet. The default price applies to all tee times.
            </p>
            <Button onClick={onAdd}>+ Add Schedule</Button>
          </div>
        ) : (
          <>
            {schedules.map((schedule) => (
              <div
                key={schedule.id}
                className={cn(
                  'flex items-center justify-between rounded-lg border p-3',
                  schedule.invalidReason && 'bg-warning/10 border-warning/30',
                )}
              >
                <div className="space-y-1 min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-sm text-ink">{schedule.name}</span>
                    {schedule.invalidReason && (
                      <Badge className="bg-warning/15 text-warning border-0 text-[10px] px-1.5 py-0">
                        Invalid
                      </Badge>
                    )}
                  </div>
                  <div className="flex items-center gap-3">
                    <DayPillsReadonly days={schedule.daysOfWeek} />
                    <span className="text-xs text-muted-foreground">
                      {formatTime(schedule.startTime)} – {formatTime(schedule.endTime)}
                    </span>
                  </div>
                  {schedule.invalidReason && (
                    <p className="text-xs text-warning">{schedule.invalidReason}</p>
                  )}
                </div>
                <div className="flex items-center gap-2 ml-4">
                  <span
                    className={cn(
                      'text-lg font-semibold',
                      schedule.invalidReason ? 'line-through text-warning' : 'text-ink',
                    )}
                  >
                    ${schedule.price.toFixed(2)}
                  </span>
                  <DropdownMenu>
                    <DropdownMenuTrigger asChild>
                      <Button variant="ghost" size="sm" className="h-8 w-8 p-0">
                        <MoreHorizontal className="h-4 w-4" />
                      </Button>
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem onClick={() => onEdit(schedule)}>Edit</DropdownMenuItem>
                      <DropdownMenuItem
                        className="text-destructive"
                        onClick={() => setDeleteTarget(schedule)}
                      >
                        Delete
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              </div>
            ))}
            <Button variant="outline" onClick={onAdd} className="w-full">
              + Add Schedule
            </Button>
          </>
        )}
      </CardContent>

      <AlertDialog open={!!deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete {deleteTarget?.name}?</AlertDialogTitle>
            <AlertDialogDescription>
              This rate schedule will be removed and the default price will apply to its time slots.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeleting}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (deleteTarget) {
                  onDelete(deleteTarget.id);
                  setDeleteTarget(null);
                }
              }}
              disabled={isDeleting}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </Card>
  );
}
