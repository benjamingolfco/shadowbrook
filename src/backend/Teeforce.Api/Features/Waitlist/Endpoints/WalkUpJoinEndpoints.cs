using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Time;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.Waitlist.Endpoints;

public static class WalkUpJoinEndpoints
{
    [WolverinePost("/walkup/verify")]
    [AllowAnonymous]
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
    [AllowAnonymous]
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

        var courseTimeZoneId = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => c.TimeZoneId)
            .FirstAsync();

        var activeCount = await db.GolferWaitlistEntries
            .CountAsync(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null);

        var entry = await waitlist.Join(golfer, entryRepo, timeProvider, courseTimeZoneId);
        entryRepo.Add(entry);

        var submittedName = $"{request.FirstName.Trim()} {request.LastName.Trim()}";

        return Results.Created($"/walkup/join", new JoinWaitlistResponse(
            entry.Id,
            golfer.Id,
            submittedName,
            activeCount + 1,
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
    Guid GolferId,
    string GolferName,
    int Position,
    string CourseName);
