import type { ReactNode } from 'react';
import {
  useSummary,
  useFillRates,
  useBookingTrends,
  usePopularTimes,
  useWaitlistStats,
} from '../hooks/useAnalytics';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { PageTopbar } from '@/components/layout/PageTopbar';
import { StatTile } from '../components/StatTile';
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';

function ChartPanel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card className="border-border-strong">
      <CardHeader>
        <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function EmptyChart() {
  return (
    <div className="flex h-[300px] items-center justify-center text-ink-muted text-sm">
      No data yet
    </div>
  );
}

function statValue(value: number | undefined, loading: boolean): ReactNode {
  if (loading) return <Skeleton className="h-7 w-12 inline-block" />;
  return value ?? '—';
}

export default function Dashboard() {
  const summary = useSummary();
  const fillRates = useFillRates();
  const bookingTrends = useBookingTrends();
  const popularTimes = usePopularTimes();
  const waitlistStats = useWaitlistStats();

  const fillRatesData = fillRates.data ?? [];
  const bookingTrendsData = bookingTrends.data ?? [];
  const popularTimesData = popularTimes.data ?? [];

  return (
    <>
      <PageTopbar
        middle={<h1 className="font-display text-[18px] text-ink">Analytics Dashboard</h1>}
      />

      {/* Summary tiles */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatTile
          label="Total Organizations"
          value={statValue(summary.data?.totalOrganizations, summary.isLoading)}
        />
        <StatTile
          label="Total Courses"
          value={statValue(summary.data?.totalCourses, summary.isLoading)}
        />
        <StatTile
          label="Active Users"
          value={statValue(summary.data?.activeUsers, summary.isLoading)}
        />
        <StatTile
          label="Bookings Today"
          value={statValue(summary.data?.bookingsToday, summary.isLoading)}
        />
      </div>

      {/* Fill Rates */}
      <div className="mb-6">
        <ChartPanel title="Fill Rates (Last 7 Days)">
          {fillRates.isLoading ? (
            <Skeleton className="h-[300px] w-full" />
          ) : fillRatesData.length === 0 ? (
            <EmptyChart />
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={fillRatesData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" />
                <YAxis unit="%" domain={[0, 100]} />
                <Tooltip formatter={(v) => (v != null ? `${v}%` : '—')} />
                <Line
                  type="monotone"
                  dataKey="fillPercentage"
                  name="Fill %"
                  stroke="var(--green)"
                  dot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </ChartPanel>
      </div>

      {/* Booking Trends + Popular Times */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        <ChartPanel title="Booking Trends (Last 30 Days)">
          {bookingTrends.isLoading ? (
            <Skeleton className="h-[300px] w-full" />
          ) : bookingTrendsData.length === 0 ? (
            <EmptyChart />
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <LineChart data={bookingTrendsData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Line
                  type="monotone"
                  dataKey="bookingCount"
                  name="Bookings"
                  stroke="var(--green)"
                  dot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </ChartPanel>

        <ChartPanel title="Popular Times">
          {popularTimes.isLoading ? (
            <Skeleton className="h-[300px] w-full" />
          ) : popularTimesData.length === 0 ? (
            <EmptyChart />
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={popularTimesData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="time" />
                <YAxis allowDecimals={false} />
                <Tooltip />
                <Bar dataKey="bookingCount" name="Bookings" fill="var(--ink)" />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartPanel>
      </div>

      {/* Waitlist Stats panel */}
      <Card className="border-border-strong">
        <CardHeader>
          <CardTitle className="text-[11px] uppercase tracking-wider text-ink-muted font-normal">
            Waitlist Stats
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatTile
              label="Active Entries"
              value={statValue(waitlistStats.data?.activeEntries, waitlistStats.isLoading)}
            />
            <StatTile
              label="Offers Sent"
              value={statValue(waitlistStats.data?.offersSent, waitlistStats.isLoading)}
            />
            <StatTile
              label="Offers Accepted"
              value={statValue(waitlistStats.data?.offersAccepted, waitlistStats.isLoading)}
            />
            <StatTile
              label="Offers Rejected"
              value={statValue(waitlistStats.data?.offersRejected, waitlistStats.isLoading)}
            />
          </div>
        </CardContent>
      </Card>
    </>
  );
}
