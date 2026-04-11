import { useMemo } from 'react';
import { Link } from 'react-router';
import { useCourseId } from '../../hooks/useCourseId';
import { useWeeklySchedule } from '../hooks/useWeeklySchedule';
import { useTeeTimeSettings } from '../hooks/useTeeTimeSettings';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';

function getMonday(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay();
  const diff = day === 0 ? 6 : day - 1;
  d.setDate(d.getDate() - diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

function formatDateParam(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export default function Dashboard() {
  const courseId = useCourseId();
  const startDate = useMemo(() => formatDateParam(getMonday(new Date())), []);
  const today = useMemo(() => formatDateParam(new Date()), []);

  const weeklyQuery = useWeeklySchedule(courseId, startDate);
  const settingsQuery = useTeeTimeSettings(courseId);

  const todayStatus = weeklyQuery.data?.days.find((d) => d.date === today);
  const isConfigured = !!settingsQuery.data?.firstTeeTime;

  const statusCounts = useMemo(() => {
    if (!weeklyQuery.data) return null;
    const counts = { published: 0, draft: 0, notStarted: 0 };
    for (const day of weeklyQuery.data.days) {
      counts[day.status]++;
    }
    return counts;
  }, [weeklyQuery.data]);

  const todayStatusLabel = todayStatus
    ? todayStatus.status === 'notStarted'
      ? 'Not Started'
      : todayStatus.status === 'draft'
        ? 'Draft'
        : 'Published'
    : 'Not Started';

  const todayBadgeVariant =
    todayStatus?.status === 'published'
      ? ('default' as const)
      : todayStatus?.status === 'draft'
        ? ('outline' as const)
        : ('secondary' as const);

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Dashboard</h1>}
      />

      <div className="grid gap-4 p-6 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Today&apos;s Tee Sheet
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={todayBadgeVariant}>{todayStatusLabel}</Badge>
            <div className="mt-3 flex gap-2">
              <Button asChild variant="outline" size="sm">
                <Link to={`/course/${courseId}/manage/schedule`}>View Schedule</Link>
              </Button>
              <Button asChild variant="default" size="sm">
                <Link to={`/course/${courseId}/pos/tee-sheet`}>Open POS</Link>
              </Button>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              This Week
            </CardTitle>
          </CardHeader>
          <CardContent>
            {statusCounts ? (
              <p className="text-sm text-ink">
                {statusCounts.published} published, {statusCounts.draft} draft,{' '}
                {statusCounts.notStarted} not started
              </p>
            ) : (
              <p className="text-sm text-ink-muted">Loading...</p>
            )}
            <Button asChild variant="outline" size="sm" className="mt-3">
              <Link to={`/course/${courseId}/manage/schedule`}>View Schedule</Link>
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
              Schedule Defaults
            </CardTitle>
          </CardHeader>
          <CardContent>
            <Badge variant={isConfigured ? 'default' : 'secondary'}>
              {isConfigured ? 'Configured' : 'Not configured'}
            </Badge>
            <Button asChild variant="outline" size="sm" className="mt-3 block">
              <Link to={`/course/${courseId}/manage/settings`}>
                {isConfigured ? 'View Settings' : 'Configure'}
              </Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    </>
  );
}
