using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Wolverine.Http;

namespace Teeforce.Api.Features.Analytics;

public static class AnalyticsEndpoints
{
    [WolverineGet("/admin/analytics/summary")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetSummary([NotBody] ApplicationDbContext db)
    {
        var summary = await db.Database.SqlQuery<PlatformSummary>(
            $"""
            SELECT
                (SELECT COUNT(*) FROM Organizations) AS TotalOrganizations,
                (SELECT COUNT(*) FROM Courses) AS TotalCourses,
                (SELECT COUNT(*) FROM AppUsers WHERE IsActive = 1) AS ActiveUsers,
                (SELECT COUNT(*) FROM Bookings WHERE CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)) AS BookingsToday
            """).FirstOrDefaultAsync();

        return Results.Ok(summary);
    }

    [WolverineGet("/admin/analytics/fill-rates")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetFillRates(
        [NotBody] ApplicationDbContext db,
        Guid? courseId = null,
        int days = 7)
    {
        var results = courseId.HasValue
            ? await db.Database.SqlQuery<FillRateResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS DATE) AS [Date],
                    SUM(t.SlotsAvailable) AS TotalSlots,
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS FilledSlots,
                    CASE WHEN SUM(t.SlotsAvailable) = 0 THEN 0
                         ELSE CAST(SUM(t.SlotsAvailable - t.SlotsRemaining) AS DECIMAL(10,2)) / SUM(t.SlotsAvailable) * 100
                    END AS FillPercentage
                FROM TeeTimeOpenings t
                WHERE t.CourseId = {courseId.Value}
                  AND CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS DATE)
                ORDER BY [Date]
                """).ToListAsync()
            : await db.Database.SqlQuery<FillRateResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS DATE) AS [Date],
                    SUM(t.SlotsAvailable) AS TotalSlots,
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS FilledSlots,
                    CASE WHEN SUM(t.SlotsAvailable) = 0 THEN 0
                         ELSE CAST(SUM(t.SlotsAvailable - t.SlotsRemaining) AS DECIMAL(10,2)) / SUM(t.SlotsAvailable) * 100
                    END AS FillPercentage
                FROM TeeTimeOpenings t
                WHERE CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS DATE)
                ORDER BY [Date]
                """).ToListAsync();

        return Results.Ok(results);
    }

    [WolverineGet("/admin/analytics/bookings")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetBookingTrends(
        [NotBody] ApplicationDbContext db,
        Guid? courseId = null,
        int days = 30)
    {
        var results = courseId.HasValue
            ? await db.Database.SqlQuery<BookingTrendResult>(
                $"""
                SELECT
                    CAST(CreatedAt AS DATE) AS [Date],
                    COUNT(*) AS BookingCount
                FROM Bookings
                WHERE CourseId = {courseId.Value}
                  AND CAST(CreatedAt AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND Status != 'Rejected'
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY [Date]
                """).ToListAsync()
            : await db.Database.SqlQuery<BookingTrendResult>(
                $"""
                SELECT
                    CAST(CreatedAt AS DATE) AS [Date],
                    COUNT(*) AS BookingCount
                FROM Bookings
                WHERE CAST(CreatedAt AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND Status != 'Rejected'
                GROUP BY CAST(CreatedAt AS DATE)
                ORDER BY [Date]
                """).ToListAsync();

        return Results.Ok(results);
    }

    [WolverineGet("/admin/analytics/popular-times")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetPopularTimes(
        [NotBody] ApplicationDbContext db,
        Guid? courseId = null,
        int days = 30)
    {
        var results = courseId.HasValue
            ? await db.Database.SqlQuery<PopularTimeResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS TIME) AS [Time],
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS BookingCount
                FROM TeeTimeOpenings t
                WHERE t.CourseId = {courseId.Value}
                  AND CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS TIME)
                HAVING SUM(t.SlotsAvailable - t.SlotsRemaining) > 0
                ORDER BY BookingCount DESC
                """).ToListAsync()
            : await db.Database.SqlQuery<PopularTimeResult>(
                $"""
                SELECT
                    CAST(t.TeeTime AS TIME) AS [Time],
                    SUM(t.SlotsAvailable - t.SlotsRemaining) AS BookingCount
                FROM TeeTimeOpenings t
                WHERE CAST(t.TeeTime AS DATE) >= CAST(DATEADD(DAY, {-days}, GETUTCDATE()) AS DATE)
                  AND t.Status != 'Cancelled'
                GROUP BY CAST(t.TeeTime AS TIME)
                HAVING SUM(t.SlotsAvailable - t.SlotsRemaining) > 0
                ORDER BY BookingCount DESC
                """).ToListAsync();

        return Results.Ok(results);
    }

    [WolverineGet("/admin/analytics/waitlist")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> GetWaitlistStats(
        [NotBody] ApplicationDbContext db,
        Guid? courseId = null)
    {
        var result = courseId.HasValue
            ? await db.Database.SqlQuery<WaitlistStatsResult>(
                $"""
                SELECT
                    (SELECT COUNT(*) FROM GolferWaitlistEntries e
                     INNER JOIN CourseWaitlists w ON e.CourseWaitlistId = w.Id
                     WHERE w.CourseId = {courseId.Value} AND e.RemovedAt IS NULL) AS ActiveEntries,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE CourseId = {courseId.Value}) AS OffersSent,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE CourseId = {courseId.Value} AND Status = 'Accepted') AS OffersAccepted,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE CourseId = {courseId.Value} AND Status = 'Rejected') AS OffersRejected
                """).FirstOrDefaultAsync()
            : await db.Database.SqlQuery<WaitlistStatsResult>(
                $"""
                SELECT
                    (SELECT COUNT(*) FROM GolferWaitlistEntries WHERE RemovedAt IS NULL) AS ActiveEntries,
                    (SELECT COUNT(*) FROM WaitlistOffers) AS OffersSent,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE Status = 'Accepted') AS OffersAccepted,
                    (SELECT COUNT(*) FROM WaitlistOffers WHERE Status = 'Rejected') AS OffersRejected
                """).FirstOrDefaultAsync();

        return Results.Ok(result);
    }
}
