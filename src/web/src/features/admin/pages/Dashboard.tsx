import {
  useSummary,
  useFillRates,
  useBookingTrends,
  usePopularTimes,
  useWaitlistStats,
} from '../hooks/useAnalytics';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
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

function StatCard({
  label,
  value,
  loading,
}: {
  label: string;
  value: number | undefined;
  loading: boolean;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-24" />
        ) : (
          <p className="text-3xl font-bold">{value ?? '—'}</p>
        )}
      </CardContent>
    </Card>
  );
}

function ChartCard({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-semibold">{title}</CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}

function EmptyChart() {
  return (
    <div className="flex h-[300px] items-center justify-center text-muted-foreground text-sm">
      No data yet
    </div>
  );
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
    <div className="space-y-6 p-6">
      <h1 className="text-2xl font-bold">Analytics Dashboard</h1>

      {/* Row 1: Summary cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatCard
          label="Total Organizations"
          value={summary.data?.totalOrganizations}
          loading={summary.isLoading}
        />
        <StatCard
          label="Total Courses"
          value={summary.data?.totalCourses}
          loading={summary.isLoading}
        />
        <StatCard
          label="Active Users"
          value={summary.data?.activeUsers}
          loading={summary.isLoading}
        />
        <StatCard
          label="Bookings Today"
          value={summary.data?.bookingsToday}
          loading={summary.isLoading}
        />
      </div>

      {/* Row 2: Fill Rates chart */}
      <ChartCard title="Fill Rates (Last 7 Days)">
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
              <Tooltip formatter={(v: number) => `${v}%`} />
              <Line
                type="monotone"
                dataKey="fillPercentage"
                name="Fill %"
                stroke="#2563eb"
                dot={false}
              />
            </LineChart>
          </ResponsiveContainer>
        )}
      </ChartCard>

      {/* Row 3: Booking Trends + Popular Times */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <ChartCard title="Booking Trends (Last 30 Days)">
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
                  stroke="#16a34a"
                  dot={false}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Popular Times">
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
                <Bar dataKey="bookingCount" name="Bookings" fill="#9333ea" />
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* Row 4: Waitlist Stats */}
      <div>
        <h2 className="text-lg font-semibold mb-3">Waitlist Stats</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            label="Active Entries"
            value={waitlistStats.data?.activeEntries}
            loading={waitlistStats.isLoading}
          />
          <StatCard
            label="Offers Sent"
            value={waitlistStats.data?.offersSent}
            loading={waitlistStats.isLoading}
          />
          <StatCard
            label="Offers Accepted"
            value={waitlistStats.data?.offersAccepted}
            loading={waitlistStats.isLoading}
          />
          <StatCard
            label="Offers Rejected"
            value={waitlistStats.data?.offersRejected}
            loading={waitlistStats.isLoading}
          />
        </div>
      </div>
    </div>
  );
}
