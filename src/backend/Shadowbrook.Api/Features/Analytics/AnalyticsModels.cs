namespace Shadowbrook.Api.Features.Analytics;

public sealed record PlatformSummary(
    int TotalOrganizations,
    int TotalCourses,
    int ActiveUsers,
    int BookingsToday);

public sealed record FillRateResult(
    DateOnly Date,
    int TotalSlots,
    int FilledSlots,
    decimal FillPercentage);

public sealed record BookingTrendResult(
    DateOnly Date,
    int BookingCount);

public sealed record PopularTimeResult(
    TimeOnly Time,
    int BookingCount);

public sealed record WaitlistStatsResult(
    int ActiveEntries,
    int OffersSent,
    int OffersAccepted,
    int OffersRejected);
