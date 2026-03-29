import { useParams, Link } from 'react-router';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';
import { useDevGolferSms } from '../hooks/useDevGolferSms';
import type { SmsMessage } from '../hooks/useDevSms';

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

export default function DevGolferSmsPage() {
  const { golferId } = useParams<{ golferId: string }>();
  const { data: messages, isLoading, isError } = useDevGolferSms(golferId ?? '');

  // Group messages by date for date separators
  const byDate: { date: string; msgs: SmsMessage[] }[] = [];
  for (const msg of messages ?? []) {
    const date = formatDate(msg.timestamp);
    const last = byDate[byDate.length - 1];
    if (last && last.date === date) {
      last.msgs.push(msg);
    } else {
      byDate.push({ date, msgs: [msg] });
    }
  }

  return (
    <div className="min-h-dvh flex flex-col items-center px-4 py-8">
      <div className="w-full max-w-md">
        <div className="mb-6">
          <h1 className="text-lg font-semibold">SMS Messages</h1>
          <p className="text-xs text-muted-foreground">Auto-refreshes every 5 seconds</p>
        </div>

        {isLoading && (
          <p className="text-muted-foreground text-sm">Loading messages...</p>
        )}

        {isError && (
          <p className="text-destructive text-sm">Failed to load SMS messages.</p>
        )}

        {!isLoading && !isError && messages?.length === 0 && (
          <p className="text-muted-foreground text-sm">
            No messages yet. SMS will appear here after booking or waitlist events.
          </p>
        )}

        {!isLoading && !isError && byDate.length > 0 && (
          <div className="flex flex-col gap-4">
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
        )}

        <div className="mt-8 text-center">
          <Link to="/admin/dev/sms" className="text-xs text-muted-foreground underline">
            View all conversations
          </Link>
        </div>
      </div>
    </div>
  );
}
