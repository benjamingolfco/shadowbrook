using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Waitlist;

public static class WalkUpJoinEndpoints
{
    [WolverinePost("/walkup/verify")]
    public static async Task<IResult> VerifyShortCode(VerifyCodeRequest request, ApplicationDbContext db, TimeProvider timeProvider)
    {
        var waitlist = await db.CourseWaitlists
            .OfType<Domain.CourseWaitlistAggregate.WalkUpWaitlist>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.ShortCode == request.Code);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "Code not found. Check the code posted at the course and try again." });
        }

        if (waitlist.Status != WaitlistStatus.Open)
        {
            return Results.Json(new { error = "This waitlist is closed." }, statusCode: StatusCodes.Status410Gone);
        }

        // Course guaranteed to exist via FK
        var course = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => new { c.Name, c.TimeZoneId })
            .FirstAsync();

        var today = CourseTime.Today(timeProvider, course.TimeZoneId);

        if (waitlist.Date != today)
        {
            return Results.NotFound(new { error = "Code not found or waitlist is not active." });
        }

        var courseName = course.Name;

        return Results.Ok(new VerifyCodeResponse(
            waitlist.Id,
            courseName,
            waitlist.ShortCode));
    }

    [WolverinePost("/walkup/join")]
    public static async Task<IResult> JoinWaitlist(
        JoinWaitlistRequest request,
        ICourseWaitlistRepository waitlistRepo,
        IGolferWaitlistEntryRepository entryRepo,
        IGolferRepository golferRepo,
        ITimeProvider timeProvider,
        ApplicationDbContext db)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await waitlistRepo.GetByIdAsync(request.CourseWaitlistId)
            as Domain.CourseWaitlistAggregate.WalkUpWaitlist;

        if (waitlist is null || waitlist.Status != WaitlistStatus.Open)
        {
            return Results.NotFound(new { error = "This waitlist is no longer accepting new entries." });
        }

        // Course guaranteed to exist via FK
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
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

        var entry = await waitlist.Join(golfer, entryRepo, timeProvider);
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

        return Results.Created($"/walkup/join", new JoinWaitlistResponse(
            entry.Id,
            submittedName,
            position,
            courseName));
    }
}

public record VerifyCodeRequest(string Code);

public class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .Length(4).WithMessage("Code must be exactly 4 digits.")
            .Must(c => c.All(char.IsDigit)).WithMessage("Code must be exactly 4 digits.");
    }
}

public record VerifyCodeResponse(
    Guid CourseWaitlistId,
    string CourseName,
    string ShortCode);

public record JoinWaitlistRequest(
    Guid CourseWaitlistId,
    string FirstName,
    string LastName,
    string Phone);

public class JoinWaitlistRequestValidator : AbstractValidator<JoinWaitlistRequest>
{
    public JoinWaitlistRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.");

        RuleFor(x => x.Phone)
            .Must(PhoneNormalizer.IsValid).WithMessage("A valid US phone number is required.");
    }
}

public record JoinWaitlistResponse(
    Guid EntryId,
    string GolferName,
    int Position,
    string CourseName);
