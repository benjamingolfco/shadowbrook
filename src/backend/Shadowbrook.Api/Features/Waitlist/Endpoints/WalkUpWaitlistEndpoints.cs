using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Waitlist.Endpoints;

public static class WalkUpWaitlistEndpoints
{
    [WolverinePost("/courses/{courseId}/walkup-waitlist/open")]
    public static async Task<IResult> OpenWaitlist(
        Guid courseId,
        [NotBody] ApplicationDbContext db,
        ICourseWaitlistRepository repo,
        IShortCodeGenerator shortCodeGenerator,
        ITimeProvider timeProvider,
        TimeProvider systemTimeProvider)
    {
        var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
        var today = CourseTime.Today(systemTimeProvider, timeZoneId);

        var waitlist = await Domain.CourseWaitlistAggregate.WalkUpWaitlist.OpenAsync(
            courseId, today, shortCodeGenerator, repo, timeProvider);

        repo.Add(waitlist);

        var response = ToResponse(waitlist);
        return Results.Created($"/courses/{courseId}/walkup-waitlist/today", response);
    }

    [WolverinePost("/courses/{courseId}/walkup-waitlist/close")]
    public static async Task<IResult> CloseWaitlist(
        Guid courseId,
        [NotBody] ApplicationDbContext db,
        ICourseWaitlistRepository repo,
        ITimeProvider timeProvider,
        TimeProvider systemTimeProvider)
    {
        var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
        var today = CourseTime.Today(systemTimeProvider, timeZoneId);

        var waitlist = await repo.GetOpenByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No open walk-up waitlist found for today." });
        }

        waitlist.Close(timeProvider);

        return Results.Ok(ToResponse(waitlist));
    }

    [WolverinePost("/courses/{courseId}/walkup-waitlist/reopen")]
    public static async Task<IResult> ReopenWaitlist(
        Guid courseId,
        [NotBody] ApplicationDbContext db,
        ICourseWaitlistRepository repo,
        TimeProvider systemTimeProvider)
    {
        var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
        var today = CourseTime.Today(systemTimeProvider, timeZoneId);

        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "No walk-up waitlist found for today." });
        }

        waitlist.Reopen();

        return Results.Ok(ToResponse(waitlist));
    }

    [WolverineGet("/courses/{courseId}/walkup-waitlist/today")]
    public static async Task<IResult> GetToday(
        Guid courseId,
        ApplicationDbContext db,
        ICourseWaitlistRepository repo,
        TimeProvider systemTimeProvider)
    {
        var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
        var today = CourseTime.Today(systemTimeProvider, timeZoneId);

        var waitlist = await repo.GetByCourseDateAsync(courseId, today);

        var waitlistResponse = waitlist is not null ? ToResponse(waitlist) : null;

        var entries = waitlist is not null
            ? (await db.GolferWaitlistEntries
                .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
                .Join(db.Golfers.IgnoreQueryFilters(),
                    e => e.GolferId, g => g.Id,
                    (e, g) => new WalkUpWaitlistEntryResponse(e.Id, g.FirstName + " " + g.LastName, e.GroupSize, e.JoinedAt))
                .ToListAsync())
                .OrderBy(e => e.JoinedAt)
                .ToList()
            : new List<WalkUpWaitlistEntryResponse>();

        var openings = (await db.TeeTimeOpenings
                .Where(o => o.CourseId == courseId && o.TeeTime.Date == today)
                .Select(o => new { o.Id, TeeTimeTime = o.TeeTime.Time, o.SlotsAvailable, o.SlotsRemaining, o.Status })
                .ToListAsync())
                .Select(o => new WalkUpWaitlistOpeningResponse(o.Id, o.TeeTimeTime.ToString("HH:mm"), o.SlotsAvailable, o.SlotsRemaining, o.Status.ToString()))
                .ToList();

        return Results.Ok(new WalkUpWaitlistTodayResponse(waitlistResponse, entries, openings));
    }

    [WolverinePost("/courses/{courseId}/tee-time-openings")]
    public static async Task<IResult> CreateOpening(
        Guid courseId,
        CreateTeeTimeOpeningRequest request,
        ApplicationDbContext db,
        ITeeTimeOpeningRepository openingRepo,
        ITimeProvider timeProvider,
        TimeProvider systemTimeProvider)
    {
        var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
        var today = CourseTime.Today(systemTimeProvider, timeZoneId);
        var parsedTeeTime = TimeOnly.ParseExact(request.TeeTime, ["HH:mm", "HH:mm:ss"]);

        var opening = TeeTimeOpening.Create(courseId, today, parsedTeeTime, request.SlotsAvailable, operatorOwned: true, timeProvider);
        openingRepo.Add(opening);

        return Results.Created(
            $"/courses/{courseId}/tee-time-openings/{opening.Id}",
            new WalkUpWaitlistOpeningResponse(opening.Id, opening.TeeTime.Time.ToString("HH:mm"), opening.SlotsAvailable, opening.SlotsRemaining, opening.Status.ToString()));
    }

    [WolverinePost("/courses/{courseId}/tee-time-openings/{openingId}/cancel")]
    public static async Task<IResult> CancelOpening(
        Guid courseId,
        Guid openingId,
        ITeeTimeOpeningRepository openingRepo,
        ITimeProvider timeProvider)
    {
        var opening = await openingRepo.GetByIdAsync(openingId);

        if (opening is null)
        {
            return Results.NotFound(new { error = "Tee time opening not found." });
        }

        if (opening.CourseId != courseId)
        {
            return Results.NotFound(new { error = "Tee time opening not found." });
        }

        opening.Cancel(timeProvider);

        return Results.Ok(new WalkUpWaitlistOpeningResponse(
            opening.Id,
            opening.TeeTime.Time.ToString("HH:mm"),
            opening.SlotsAvailable,
            opening.SlotsRemaining,
            opening.Status.ToString()));
    }

    [WolverinePost("/courses/{courseId}/walkup-waitlist/entries")]
    public static async Task<IResult> AddGolferToWaitlist(
        Guid courseId,
        AddGolferToWaitlistRequest request,
        ApplicationDbContext db,
        ICourseWaitlistRepository repo,
        IGolferWaitlistEntryRepository entryRepo,
        IGolferRepository golferRepo,
        ITimeProvider timeProvider,
        TimeProvider systemTimeProvider)
    {
        var timeZoneId = await db.Courses.Where(c => c.Id == courseId).Select(c => c.TimeZoneId).FirstAsync();
        var today = CourseTime.Today(systemTimeProvider, timeZoneId);
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

        // Golfer lookup-or-create
        var golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

        if (golfer is null)
        {
            golfer = Golfer.Create(normalizedPhone!, request.FirstName, request.LastName);
            golferRepo.Add(golfer);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race condition: another request created the golfer concurrently
                db.Entry(golfer).State = EntityState.Detached;
                golfer = await golferRepo.GetByPhoneAsync(normalizedPhone!);

                if (golfer is null)
                {
                    return Results.Problem("Unable to create or retrieve golfer record.", statusCode: 500);
                }
            }
        }

        var entry = await waitlist.Join(golfer, entryRepo, timeProvider, timeZoneId, request.GroupSize ?? 1);
        entryRepo.Add(entry);

        // Intentional mid-flow save: position query reads from DB,
        // so the new entry must be flushed first. This mirrors the golfer
        // upsert pattern above. Wolverine won't double-save a clean tracker.
        await db.SaveChangesAsync();

        var joinedAt = entry.JoinedAt;
        var activeEntries = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();
        var position = activeEntries.Count(t => t <= joinedAt);

        var submittedName = $"{request.FirstName.Trim()} {request.LastName.Trim()}";

        return Results.Created(
            $"/courses/{courseId}/walkup-waitlist/entries/{entry.Id}",
            new AddGolferToWaitlistResponse(
                entry.Id,
                submittedName,
                golfer.Phone,
                entry.GroupSize,
                position,
                courseName));
    }

    private static WalkUpWaitlistResponse ToResponse(Domain.CourseWaitlistAggregate.WalkUpWaitlist w) =>
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

public record CreateTeeTimeOpeningRequest(string TeeTime, int SlotsAvailable);

public class CreateTeeTimeOpeningRequestValidator : AbstractValidator<CreateTeeTimeOpeningRequest>
{
    public CreateTeeTimeOpeningRequestValidator()
    {
        RuleFor(x => x.TeeTime)
            .NotEmpty().WithMessage("Tee time is required.")
            .Must(t => TimeOnly.TryParseExact(t, ["HH:mm", "HH:mm:ss"], out _))
            .WithMessage("A valid tee time in HH:mm format is required.");

        RuleFor(x => x.SlotsAvailable)
            .InclusiveBetween(1, 4)
            .WithMessage("Slots available must be between 1 and 4.");
    }
}

public record WalkUpWaitlistOpeningResponse(
    Guid Id,
    string TeeTime,
    int SlotsAvailable,
    int SlotsRemaining,
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
    List<WalkUpWaitlistEntryResponse> Entries,
    List<WalkUpWaitlistOpeningResponse> Openings);

public record WalkUpWaitlistEntryResponse(
    Guid Id,
    string GolferName,
    int GroupSize,
    DateTimeOffset JoinedAt);
