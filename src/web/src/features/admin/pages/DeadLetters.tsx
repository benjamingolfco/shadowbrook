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
      <TableCell colSpan={5} className="bg-muted/40 px-6 py-4">
        <div className="space-y-3" data-testid="dead-letter-detail">
          <div>
            <p className="text-xs font-semibold uppercase text-muted-foreground mb-1">
              Exception Message
            </p>
            <p className="text-sm whitespace-pre-wrap break-words">{envelope.exceptionMessage}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase text-muted-foreground mb-1">
              Message Body
            </p>
            <pre className="text-xs bg-background border rounded-md p-3 overflow-x-auto whitespace-pre-wrap break-words">
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

  if (isLoading) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading dead letter messages...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <p className="text-destructive">
          Error: {error instanceof Error ? error.message : 'Failed to load dead letter messages'}
        </p>
      </div>
    );
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold font-[family-name:var(--font-heading)]">
            Dead Letter Queue
          </h1>
          <p className="text-sm text-muted-foreground">
            {page ? `${page.totalCount} failed messages` : 'Messages that failed processing'}
          </p>
        </div>

        {selectedIds.size > 0 && (
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">
              {selectedIds.size} selected
            </span>
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
        )}
      </div>

      {envelopes.length === 0 ? (
        <div className="border rounded-md p-12 text-center">
          <p className="text-muted-foreground text-sm">No dead letter messages. All clear.</p>
        </div>
      ) : (
        <div className="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-10">
                  <Checkbox
                    checked={allSelected}
                    data-state={someSelected ? 'indeterminate' : undefined}
                    onCheckedChange={(checked) => handleSelectAll(checked === true)}
                    aria-label="Select all messages"
                  />
                </TableHead>
                <TableHead>Message Type</TableHead>
                <TableHead>Exception Type</TableHead>
                <TableHead className="hidden md:table-cell">Exception Message</TableHead>
                <TableHead className="hidden md:table-cell whitespace-nowrap">Sent At</TableHead>
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
                    <TableCell className="font-medium font-mono text-sm">
                      {stripNamespace(envelope.messageType)}
                    </TableCell>
                    <TableCell className="text-sm text-destructive">
                      {stripNamespace(envelope.exceptionType)}
                    </TableCell>
                    <TableCell className="hidden md:table-cell text-sm text-muted-foreground">
                      {truncate(envelope.exceptionMessage)}
                    </TableCell>
                    <TableCell className="hidden md:table-cell text-sm text-muted-foreground whitespace-nowrap">
                      {formatSentAt(envelope.sentAt)}
                    </TableCell>
                  </TableRow>
                  {expandedIds.has(envelope.id) && (
                    <ExpandedRow envelope={envelope} />
                  )}
                </React.Fragment>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  );
}
