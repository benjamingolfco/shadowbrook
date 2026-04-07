import React, { useState } from 'react';
import { useDeadLetters, useReplayDeadLetters, useDeleteDeadLetters } from '../hooks/useDeadLetters';
import type { DeadLetterEnvelope } from '../hooks/useDeadLetters';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { PageTopbar } from '@/components/layout/PageTopbar';

function stripNamespace(typeName: string | undefined): string {
  if (!typeName) return 'Unknown';
  const parts = typeName.split('.');
  return parts[parts.length - 1] ?? typeName;
}

function truncate(text: string | undefined, maxLength = 80): string {
  if (!text) return '';
  if (text.length <= maxLength) return text;
  return text.slice(0, maxLength) + '\u2026';
}

function formatSentAt(isoString: string | undefined): string {
  if (!isoString) return '';
  return new Date(isoString).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

interface ExpandedRowProps {
  envelope: DeadLetterEnvelope;
}

function ExpandedRow({ envelope }: ExpandedRowProps) {
  return (
    <TableRow>
      <TableCell colSpan={5} className="bg-canvas px-6 py-4">
        <div className="space-y-3" data-testid="dead-letter-detail">
          <div>
            <p className="text-[11px] uppercase tracking-wider text-ink-muted mb-1">
              Exception Message
            </p>
            <p className="text-sm whitespace-pre-wrap break-words text-ink">
              {envelope.exceptionMessage}
            </p>
          </div>
          <div>
            <p className="text-[11px] uppercase tracking-wider text-ink-muted mb-1">
              Message Body
            </p>
            <pre className="font-mono text-[12px] bg-canvas border border-border-strong rounded-md p-3 overflow-x-auto whitespace-pre-wrap break-words">
              {JSON.stringify(envelope.message, null, 2)}
            </pre>
          </div>
        </div>
      </TableCell>
    </TableRow>
  );
}

export default function DeadLetters() {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const { data, isLoading, error } = useDeadLetters();
  const replay = useReplayDeadLetters();
  const deleteMessages = useDeleteDeadLetters();

  const page = data?.[0];
  const envelopes = page?.envelopes ?? [];

  function handleSelectAll(checked: boolean) {
    if (checked) {
      setSelectedIds(new Set(envelopes.map((e) => e.id)));
    } else {
      setSelectedIds(new Set());
    }
  }

  function handleSelectOne(id: string, checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  function handleToggleExpand(id: string) {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function handleReplay() {
    replay.mutate(Array.from(selectedIds), {
      onSuccess: () => {
        setSelectedIds(new Set());
      },
    });
  }

  function handleDelete() {
    deleteMessages.mutate(Array.from(selectedIds), {
      onSuccess: () => {
        setSelectedIds(new Set());
      },
    });
  }

  const allSelected = envelopes.length > 0 && selectedIds.size === envelopes.length;
  const someSelected = selectedIds.size > 0 && !allSelected;
  const totalCount = page?.totalCount ?? 0;

  const topbarMiddle = (
    <h1 className="font-display text-[18px] text-ink">
      Dead Letter Queue
      {totalCount > 0 && (
        <span className="ml-2 font-mono text-[13px] text-ink-muted">· {totalCount}</span>
      )}
    </h1>
  );

  const topbarRight =
    selectedIds.size > 0 ? (
      <div className="flex items-center gap-2">
        <span className="text-[12px] text-ink-muted">{selectedIds.size} selected</span>
        <Button
          variant="outline"
          size="sm"
          onClick={handleReplay}
          disabled={replay.isPending}
        >
          {replay.isPending ? 'Replaying\u2026' : 'Replay'}
        </Button>
        <AlertDialog>
          <AlertDialogTrigger asChild>
            <Button variant="destructive" size="sm" disabled={deleteMessages.isPending}>
              {deleteMessages.isPending ? 'Deleting\u2026' : 'Delete'}
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Delete dead letter messages?</AlertDialogTitle>
              <AlertDialogDescription>
                This will permanently delete {selectedIds.size}{' '}
                {selectedIds.size === 1 ? 'message' : 'messages'}. This action cannot be
                undone.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction onClick={handleDelete}>Delete</AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    ) : null;

  if (isLoading) {
    return (
      <>
        <PageTopbar middle={topbarMiddle} />
        <p className="text-ink-muted text-sm py-12 text-center">
          Loading dead letter messages...
        </p>
      </>
    );
  }

  if (error) {
    return (
      <>
        <PageTopbar middle={topbarMiddle} />
        <p className="text-destructive text-sm py-12 text-center">
          Error: {error instanceof Error ? error.message : 'Failed to load dead letter messages'}
        </p>
      </>
    );
  }

  return (
    <>
      <PageTopbar middle={topbarMiddle} right={topbarRight} />

      {envelopes.length === 0 ? (
        <p className="text-ink-muted text-sm py-12 text-center">
          No dead letter messages. All clear.
        </p>
      ) : (
        <div className="border border-border-strong rounded-md bg-white overflow-hidden">
          <Table>
            <TableHeader>
              <TableRow className="bg-canvas">
                <TableHead className="w-10">
                  <Checkbox
                    checked={allSelected}
                    data-state={someSelected ? 'indeterminate' : undefined}
                    onCheckedChange={(checked) => handleSelectAll(checked === true)}
                    aria-label="Select all messages"
                  />
                </TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">
                  Message Type
                </TableHead>
                <TableHead className="text-[10px] uppercase tracking-wider text-ink-muted">
                  Exception Type
                </TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted">
                  Exception Message
                </TableHead>
                <TableHead className="hidden md:table-cell text-[10px] uppercase tracking-wider text-ink-muted whitespace-nowrap">
                  Sent At
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {envelopes.map((envelope) => (
                <React.Fragment key={envelope.id}>
                  <TableRow
                    className="cursor-pointer"
                    onClick={(e) => {
                      const target = e.target as HTMLElement;
                      if (target.closest('[role="checkbox"]')) return;
                      handleToggleExpand(envelope.id);
                    }}
                  >
                    <TableCell onClick={(e) => e.stopPropagation()}>
                      <Checkbox
                        checked={selectedIds.has(envelope.id)}
                        onCheckedChange={(checked) =>
                          handleSelectOne(envelope.id, checked === true)
                        }
                        aria-label={`Select message ${envelope.id}`}
                      />
                    </TableCell>
                    <TableCell className="font-medium font-mono text-[13px] text-ink">
                      {stripNamespace(envelope.messageType)}
                    </TableCell>
                    <TableCell className="text-[13px] text-destructive">
                      {stripNamespace(envelope.exceptionType)}
                    </TableCell>
                    <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted">
                      {truncate(envelope.exceptionMessage)}
                    </TableCell>
                    <TableCell className="hidden md:table-cell font-mono text-[12px] text-ink-muted whitespace-nowrap">
                      {formatSentAt(envelope.sentAt)}
                    </TableCell>
                  </TableRow>
                  {expandedIds.has(envelope.id) && <ExpandedRow envelope={envelope} />}
                </React.Fragment>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </>
  );
}
