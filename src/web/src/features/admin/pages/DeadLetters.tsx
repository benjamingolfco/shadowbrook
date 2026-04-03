import React, { useState, useEffect } from 'react';
import { useDeadLetters, useReplayDeadLetters, useDeleteDeadLetters } from '../hooks/useDeadLetters';
import type { DeadLetterMessage } from '../hooks/useDeadLetters';
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

function stripNamespace(typeName: string): string {
  const parts = typeName.split('.');
  return parts[parts.length - 1] ?? typeName;
}

function truncate(text: string, maxLength = 80): string {
  if (text.length <= maxLength) return text;
  return text.slice(0, maxLength) + '\u2026';
}

function formatSentAt(isoString: string): string {
  return new Date(isoString).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

interface ExpandedRowProps {
  message: DeadLetterMessage;
}

function ExpandedRow({ message }: ExpandedRowProps) {
  return (
    <TableRow>
      <TableCell colSpan={5} className="bg-muted/40 px-6 py-4">
        <div className="space-y-3" data-testid="dead-letter-detail">
          <div>
            <p className="text-xs font-semibold uppercase text-muted-foreground mb-1">
              Exception Message
            </p>
            <p className="text-sm whitespace-pre-wrap break-words">{message.ExceptionMessage}</p>
          </div>
          <div>
            <p className="text-xs font-semibold uppercase text-muted-foreground mb-1">
              Message Body
            </p>
            <pre className="text-xs bg-background border rounded-md p-3 overflow-x-auto whitespace-pre-wrap break-words">
              {JSON.stringify(message.Body, null, 2)}
            </pre>
          </div>
        </div>
      </TableCell>
    </TableRow>
  );
}

export default function DeadLetters() {
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [allMessages, setAllMessages] = useState<DeadLetterMessage[]>([]);
  const [nextId, setNextId] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  const { data, isLoading, error } = useDeadLetters(cursor);
  const replay = useReplayDeadLetters();
  const deleteMessages = useDeleteDeadLetters();

  useEffect(() => {
    if (!data) return;
    if (cursor === undefined) {
      // Initial load or post-action refresh — replace all messages
      setAllMessages(data.Messages);
    } else {
      // Pagination — append
      setAllMessages((prev) => [...prev, ...data.Messages]);
    }
    setNextId(data.NextId);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data]);

  function handleSelectAll(checked: boolean) {
    if (checked) {
      setSelectedIds(new Set(allMessages.map((m) => m.Id)));
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
        // Reset cursor so TanStack Query refetches from the beginning.
        // Setting cursor to undefined triggers the useEffect to replace allMessages.
        setCursor(undefined);
      },
    });
  }

  function handleDelete() {
    deleteMessages.mutate(Array.from(selectedIds), {
      onSuccess: () => {
        setSelectedIds(new Set());
        setCursor(undefined);
      },
    });
  }

  function handleLoadMore() {
    if (nextId) {
      setCursor(nextId);
    }
  }

  const allSelected = allMessages.length > 0 && selectedIds.size === allMessages.length;
  const someSelected = selectedIds.size > 0 && !allSelected;

  if (isLoading && allMessages.length === 0) {
    return (
      <div className="p-6">
        <p className="text-muted-foreground">Loading dead letter messages...</p>
      </div>
    );
  }

  if (error && allMessages.length === 0) {
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
            Messages that failed processing and were not retried
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

      {allMessages.length === 0 && !isLoading ? (
        <div className="border rounded-md p-12 text-center">
          <p className="text-muted-foreground text-sm">No dead letter messages. All clear.</p>
        </div>
      ) : (
        <>
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
                {allMessages.map((message) => (
                  <React.Fragment key={message.Id}>
                    <TableRow
                      className="cursor-pointer"
                      onClick={(e) => {
                        const target = e.target as HTMLElement;
                        if (target.closest('[role="checkbox"]')) return;
                        handleToggleExpand(message.Id);
                      }}
                    >
                      <TableCell onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={selectedIds.has(message.Id)}
                          onCheckedChange={(checked) =>
                            handleSelectOne(message.Id, checked === true)
                          }
                          aria-label={`Select message ${message.Id}`}
                        />
                      </TableCell>
                      <TableCell className="font-medium font-mono text-sm">
                        {stripNamespace(message.MessageType)}
                      </TableCell>
                      <TableCell className="text-sm text-destructive">
                        {stripNamespace(message.ExceptionType)}
                      </TableCell>
                      <TableCell className="hidden md:table-cell text-sm text-muted-foreground">
                        {truncate(message.ExceptionMessage)}
                      </TableCell>
                      <TableCell className="hidden md:table-cell text-sm text-muted-foreground whitespace-nowrap">
                        {formatSentAt(message.SentAt)}
                      </TableCell>
                    </TableRow>
                    {expandedIds.has(message.Id) && (
                      <ExpandedRow message={message} />
                    )}
                  </React.Fragment>
                ))}
              </TableBody>
            </Table>
          </div>

          {nextId && (
            <div className="flex justify-center">
              <Button variant="outline" onClick={handleLoadMore} disabled={isLoading}>
                {isLoading ? 'Loading\u2026' : 'Load more'}
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
