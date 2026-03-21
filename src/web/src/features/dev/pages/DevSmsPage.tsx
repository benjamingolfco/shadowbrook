import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import { useDevSms, type SmsMessage } from '../hooks/useDevSms';

const SYSTEM_NUMBER = '+10000000000';

function formatTime(timestamp: string): string {
  return new Date(timestamp).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

function formatDate(timestamp: string): string {
  return new Date(timestamp).toLocaleDateString([], {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

function getConversationPhoneNumber(msg: SmsMessage): string {
  return msg.direction === 0 ? msg.to : msg.from;
}

function groupByPhoneNumber(messages: SmsMessage[]): Map<string, SmsMessage[]> {
  const groups = new Map<string, SmsMessage[]>();
  for (const msg of messages) {
    const phone = getConversationPhoneNumber(msg);
    if (phone === SYSTEM_NUMBER) continue;
    const existing = groups.get(phone) ?? [];
    existing.push(msg);
    groups.set(phone, existing);
  }
  // Sort each group by timestamp ascending for conversation view
  for (const [phone, msgs] of groups) {
    groups.set(phone, [...msgs].sort((a, b) => a.timestamp.localeCompare(b.timestamp)));
  }
  return groups;
}

function MessageBubble({ msg }: { msg: SmsMessage }) {
  const isOutbound = msg.direction === 0;
  return (
    <div className={cn('flex flex-col gap-1', isOutbound ? 'items-end' : 'items-start')}>
      <div
        className={cn(
          'max-w-xs rounded-2xl px-4 py-2 text-sm',
          isOutbound
            ? 'bg-primary text-primary-foreground rounded-br-sm'
            : 'bg-muted text-foreground rounded-bl-sm'
        )}
      >
        {msg.body}
      </div>
      <span className="text-xs text-muted-foreground px-1">
        {isOutbound ? 'System' : 'Golfer'} · {formatTime(msg.timestamp)}
      </span>
    </div>
  );
}

function ConversationThread({ messages }: { messages: SmsMessage[] }) {
  if (messages.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-muted-foreground text-sm">
        No messages
      </div>
    );
  }

  // Group consecutive messages by date for date separators
  const byDate: { date: string; msgs: SmsMessage[] }[] = [];
  for (const msg of messages) {
    const date = formatDate(msg.timestamp);
    const last = byDate[byDate.length - 1];
    if (last && last.date === date) {
      last.msgs.push(msg);
    } else {
      byDate.push({ date, msgs: [msg] });
    }
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      {byDate.map(({ date, msgs }) => (
        <div key={date} className="flex flex-col gap-3">
          <div className="flex items-center gap-2">
            <Separator className="flex-1" />
            <span className="text-xs text-muted-foreground shrink-0">{date}</span>
            <Separator className="flex-1" />
          </div>
          {msgs.map((msg, i) => (
            <MessageBubble key={`${msg.timestamp}-${i}`} msg={msg} />
          ))}
        </div>
      ))}
    </div>
  );
}

export default function DevSmsPage() {
  const { data: messages, isLoading, isError } = useDevSms();
  const [selectedPhone, setSelectedPhone] = useState<string | null>(null);

  const groups = messages ? groupByPhoneNumber(messages) : new Map<string, SmsMessage[]>();
  const phoneNumbers = [...groups.keys()];

  // Auto-select the first phone number once data loads
  const activePhone = selectedPhone ?? phoneNumbers[0] ?? null;
  const activeMessages = activePhone ? (groups.get(activePhone) ?? []) : [];

  return (
    <div className="p-6 h-full flex flex-col gap-4">
      <div>
        <h1 className="text-2xl font-bold">Dev SMS Viewer</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Captured SMS messages — auto-refreshes every 5 seconds.
        </p>
      </div>

      {isLoading && (
        <p className="text-muted-foreground text-sm">Loading messages...</p>
      )}

      {isError && (
        <p className="text-destructive text-sm">Failed to load SMS messages.</p>
      )}

      {!isLoading && !isError && phoneNumbers.length === 0 && (
        <p className="text-muted-foreground text-sm">
          No messages yet. Trigger a booking or waitlist event to see SMS output here.
        </p>
      )}

      {!isLoading && !isError && phoneNumbers.length > 0 && (
        <div className="flex gap-4 flex-1 min-h-0">
          {/* Phone number list */}
          <Card className="w-64 shrink-0 flex flex-col">
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground uppercase tracking-wide">
                Conversations
              </CardTitle>
            </CardHeader>
            <CardContent className="flex-1 overflow-y-auto p-0">
              <ul>
                {phoneNumbers.map((phone) => {
                  const msgs = groups.get(phone) ?? [];
                  const isActive = phone === activePhone;
                  return (
                    <li key={phone}>
                      <button
                        type="button"
                        onClick={() => setSelectedPhone(phone)}
                        className={cn(
                          'w-full text-left px-4 py-3 flex items-center justify-between gap-2 hover:bg-muted/50 transition-colors',
                          isActive && 'bg-muted'
                        )}
                        aria-current={isActive ? 'true' : undefined}
                      >
                        <span className="text-sm font-mono truncate">{phone}</span>
                        <Badge variant="secondary" className="shrink-0 text-xs">
                          {msgs.length}
                        </Badge>
                      </button>
                    </li>
                  );
                })}
              </ul>
            </CardContent>
          </Card>

          {/* Conversation thread */}
          <Card className="flex-1 flex flex-col min-h-0">
            <CardHeader className="pb-2 shrink-0">
              <CardTitle className="text-sm font-mono text-muted-foreground">
                {activePhone ?? 'Select a conversation'}
              </CardTitle>
            </CardHeader>
            <Separator />
            <CardContent className="flex-1 overflow-y-auto p-0">
              <ConversationThread messages={activeMessages} />
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}
