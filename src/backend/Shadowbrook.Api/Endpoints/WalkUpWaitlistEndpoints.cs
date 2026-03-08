using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Endpoints.Filters;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpWaitlistEndpoints
{
    public static void MapWalkUpWaitlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/courses/{courseId:guid}/walkup-waitlist")
            .AddEndpointFilter<CourseExistsFilter>();

        group.MapPost("/open", OpenWaitlist);
        group.MapPost("/close", CloseWaitlist);
        group.MapGet("/today", GetToday);
        group.MapPost("/requests", CreateWaitlistRequest);
        group.MapPost("/entries", AddGolferToWaitlist);
    }

    private static async Task<IResult> OpenWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await WalkUpWaitlistEntity.OpenAsync(courseId, today, shortCodeGenerator, repo);

        repo.Add(waitlist);
        await repo.SaveAsync();

        var response = ToResponse(waitlist);
        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", response);
    }

    private static async Task<IResult> CloseWaitlist(
        Guid courseId,
        IWalkUpWaitlistRepository repo)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        waitlist.Close();

        await repo.SaveAsync();

        return Results.Ok(ToResponse(waitlist));
    }

    private static async Task<IResult> GetToday(
        Guid courseId,
        IWalkUpWaitlistRepository repo,
        ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

        var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;

        var entries = waitlist is not null
            ? (await db.GolferWaitlistEntries
                .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
                .Select(e => new WalkUpWaitlistEntryResponse(e.Id, e.GolferName, e.GroupSize, e.JoinedAt))
                .ToListAsync())
                .OrderBy(e => e.JoinedAt)
                .ToList()
            : new List<WalkUpWaitlistEntryResponse>();

        return Results.Ok(new WalkUpWaitlistTodayResponse(waitlistResponse, entries));
    }

    private static async Task<IResult> CreateWaitlistRequest(
        Guid courseId,
        CreateWalkUpWaitlistRequestRequest request,
        IWalkUpWaitlistRepository repo)
    {
        var parsedDate = DateOnly.ParseExact(request.Date, "yyyy-MM-dd");
        var parsedTeeTime = TimeOnly.ParseExact(request.TeeTime, ["HH:mm", "HH:mm:ss"]);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, parsedDate);

        if (waitlist is null)
        {
            return Results.BadRequest(new { error = "No open walk-up waitlist found for this date." });
        }

        var teeTimeRequest = waitlist.AddTeeTimeRequest(parsedTeeTime, request.GolfersNeeded);
        await repo.SaveAsync();

        var response = new WalkUpWaitlistRequestResponse(
            teeTimeRequest.Id,
            teeTimeRequest.TeeTime.ToString("HH:mm"),
            teeTimeRequest.GolfersNeeded,
            teeTimeRequest.Status.ToString());

        return Results.Created($"/courses/{courseId}/walkup-waitlist/requests/{teeTimeRequest.Id}", response);
    }

    private static async Task<IResult> AddGolferToWaitlist(
        Guid courseId,
        AddGolferToWaitlistRequest request,
        IWalkUpWaitlistRepository repo,
        ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        var courseName = await db.Courses
            .Where(c => c.Id == courseId)
            .Select(c => c.Name)
            .FirstAsync();

        // Check for existing active entry (duplicate prevention)
        var existingEntry = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.GolferPhone == normalizedPhone && e.RemovedAt == null)
            .FirstOrDefaultAsync();

        if (existingEntry is not null)
        {
            var existingPosition = await CalculatePositionAsync(db, waitlist.Id, existingEntry.JoinedAt);
            return Results.Conflict(new { error = "This golfer is already on the waitlist.", position = existingPosition });
        }

        // Golfer lookup-or-create
        var golfer = await db.Golfers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

        if (golfer is null)
        {
            var now = DateTimeOffset.UtcNow;
            golfer = new Golfer
            {
                Id = Guid.CreateVersion7(),
                Phone = normalizedPhone!,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Golfers.Add(golfer);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: another request created the golfer concurrently
                db.Golfers.Remove(golfer);
                golfer = await db.Golfers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(g => g.Phone == normalizedPhone);

                if (golfer is null)
                {
                    return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
                }
            }
        }

        var entryNow = DateTimeOffset.UtcNow;
        var entry = new GolferWaitlistEntry(Guid.CreateVersion7())
        {
            CourseWaitlistId = waitlist.Id,
            GolferId = golfer.Id,
            GolferName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            GolferPhone = normalizedPhone!,
            GroupSize = request.GroupSize ?? 1,
            IsWalkUp = true,
            IsReady = true,
            JoinedAt = entryNow,
            CreatedAt = entryNow,
            UpdatedAt = entryNow
        };

        db.GolferWaitlistEntries.Add(entry);
        await db.SaveChangesAsync();

        var position = await CalculatePositionAsync(db, waitlist.Id, entry.JoinedAt);

        entry.RaiseJoinedEvent(courseName, position);
        await db.SaveChangesAsync();

        return Results.Created(
            $"/courses/{courseId}/walkup-waitlist/entries/{entry.Id}",
            new AddGolferToWaitlistResponse(
                entry.Id,
                entry.GolferName,
                entry.GolferPhone,
                entry.GroupSize,
                position,
                courseName));
    }

    // Position is determined by how many active entries joined at or before the given timestamp.
    // We load JoinedAt values client-side to avoid DateTimeOffset translation issues with SQLite.
    private static async Task<int> CalculatePositionAsync(ApplicationDbContext db, Guid courseWaitlistId, DateTimeOffset joinedAt)
    {
        var joinedAtValues = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == courseWaitlistId && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();

        return joinedAtValues.Count(t => t <= joinedAt);
    }

    private static WalkUpWaitlistResponse ToResponse(WalkUpWaitlistEntity w) =>
        new(w.Id, w.CourseId, w.ShortCode, w.Date.ToString("yyyy-MM-dd"), w.Status.ToString(), w.OpenedAt, w.ClosedAt);
}

public record AddGolferToWaitlistRequest(string FirstName, string LastName, string Phone, int? GroupSize);

public class AddGolferToWaitlistRequestValidator : AbstractValidator<AddGolferToWaitlistRequest>
{
    public AddGolferToWaitlistRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.Phone)
            .Must(PhoneNormalizer.IsValid).WithMessage("A valid US phone number is required.");

        RuleFor(x => x.GroupSize)
            .InclusiveBetween(1, 4).WithMessage("Group size must be between 1 and 4.")
            .When(x => x.GroupSize.HasValue);
    }
}

public record AddGolferToWaitlistResponse(
    Guid EntryId,
    string GolferName,
    string GolferPhone,
    int GroupSize,
    int Position,
    string CourseName);

public record CreateWalkUpWaitlistRequestRequest(string Date, string TeeTime, int GolfersNeeded);

public class CreateWalkUpWaitlistRequestRequestValidator : AbstractValidator<CreateWalkUpWaitlistRequestRequest>
{
    public CreateWalkUpWaitlistRequestRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.")
            .Must(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", out _))
            .WithMessage("A valid date in yyyy-MM-dd format is required.");

        RuleFor(x => x.TeeTime)
            .NotEmpty().WithMessage("Tee time is required.")
            .Must(t => TimeOnly.TryParseExact(t, ["HH:mm", "HH:mm:ss"], out _))
            .WithMessage("A valid tee time in HH:mm format is required.");

        RuleFor(x => x.GolfersNeeded)
            .InclusiveBetween(1, 4)
            .WithMessage("Golfers needed must be between 1 and 4.");
    }
}

public record WalkUpWaitlistRequestResponse(
    Guid Id,
    string TeeTime,
    int GolfersNeeded,
    string Status);

public record WalkUpWaitlistResponse(
    Guid Id,
    Guid CourseId,
    string ShortCode,
    string Date,
    string Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record WalkUpWaitlistTodayResponse(
    WalkUpWaitlistResponse? Waitlist,
    List<WalkUpWaitlistEntryResponse> Entries);

public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);
