namespace Teeforce.Api.IntegrationTests;

// Shared response records used across multiple scenario test classes.
// Scenario-specific DTOs can still be defined as private records in the test class.

public record TenantIdResponse(Guid Id);

public record TenantResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TenantListResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    int CourseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TenantDetailResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    List<CourseInfo> Courses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CourseInfo(Guid Id, string Name, string? City, string? State);

public record CourseIdResponse(Guid Id);

public record CourseResponse(
    Guid Id,
    string Name,
    string? StreetAddress,
    string? City,
    string? State,
    string? ZipCode,
    string? ContactEmail,
    string? ContactPhone,
    string TimeZoneId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TenantSummary? Tenant = null);

public record TenantSummary(Guid Id, string OrganizationName);

public record WaitlistResponse(
    Guid Id,
    Guid CourseId,
    string ShortCode,
    string Date,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record WaitlistTodayResponse(
    WaitlistResponse? Waitlist,
    List<WaitlistEntryResponse> Entries);

public record WaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);

public record AddGolferResponse(
    Guid EntryId,
    string GolferName,
    string GolferPhone,
    int GroupSize,
    string CourseName);

public record ErrorResponse(string Error);

public record TeeTimeSettingsResponse(
    int TeeTimeIntervalMinutes,
    string FirstTeeTime,
    string LastTeeTime,
    int DefaultCapacity);

public record PricingResponse(decimal? DefaultPrice, decimal? MinPrice, decimal? MaxPrice, List<object>? Schedules);
public record UpdateDefaultPriceResponse(decimal? DefaultPrice);

public record TeeSheetResponse(
    Guid CourseId,
    string CourseName,
    string? Status,
    List<TeeSheetSlot> Slots);

public record TeeSheetSlot(
    DateTime TeeTime,
    string Status,
    string? GolferName,
    int PlayerCount);

public record VerifyCodeResponse(Guid CourseWaitlistId, string CourseName, string ShortCode);

public record JoinWaitlistResponse(Guid EntryId, Guid GolferId, string GolferName, int Position, string CourseName);

public record CurrentUserResponse(Guid? OrganizationId);

public record OrganizationResponse(Guid Id, string Name);

public record BulkDraftItem(DateOnly Date, Guid TeeSheetId);
public record BulkDraftResponse(List<BulkDraftItem> TeeSheets);
public record DayStatusResponse(DateOnly Date, string Status, Guid? TeeSheetId, int? IntervalCount);
public record WeeklyStatusResponse(DateOnly WeekStart, DateOnly WeekEnd, List<DayStatusResponse> Days);
