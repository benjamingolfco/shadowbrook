using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;
using Wolverine.Http;

namespace Shadowbrook.Api.Endpoints;

public static class WalkUpJoinEndpoints
{
    [WolverinePost("/walkup/verify")]
    public static async Task<IResult> VerifyShortCode(VerifyCodeRequest request, ApplicationDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var waitlist = await db.WalkUpWaitlists
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.ShortCode == request.Code && w.Date == today && w.Status == WaitlistStatus.Open);

        if (waitlist is null)
        {
            return Results.NotFound(new { error = "Code not found or waitlist is not active." });
        }

        // Course guaranteed to exist via FK
        var courseName = await db.Courses
            .IgnoreQueryFilters()
            .Where(c => c.Id == waitlist.CourseId)
            .Select(c => c.Name)
            .FirstAsync();

        return Results.Ok(new VerifyCodeResponse(
            waitlist.Id,
            courseName,
            waitlist.ShortCode));
    }

    [WolverinePost("/walkup/join")]
    public static async Task<IResult> JoinWaitlist(
        JoinWaitlistRequest request,
        IWalkUpWaitlistRepository waitlistRepo,
        IGolferWaitlistEntryRepository entryRepo,
        IGolferRepository golferRepo,
        ApplicationDbContext db)
    {
        var normalizedPhone = PhoneNormalizer.Normalize(request.Phone);

        var waitlist = await waitlistRepo.GetByIdAsync(request.CourseWaitlistId);

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

        var duplicate = await entryRepo.GetActiveByWaitlistAndGolferAsync(waitlist.Id, golfer.Id);
        if (duplicate is not null)
        {
            throw new GolferAlreadyOnWaitlistException(golfer.Phone);
        }

        var entry = waitlist.AddGolfer(golfer);
        entryRepo.Add(entry);

        var joinedAt = entry.JoinedAt;
        var activeEntries = await db.GolferWaitlistEntries
            .Where(e => e.CourseWaitlistId == waitlist.Id && e.RemovedAt == null)
            .Select(e => e.JoinedAt)
            .ToListAsync();
        var position = activeEntries.Count(t => t <= joinedAt);

        return Results.Created($"/walkup/join", new JoinWaitlistResponse(
            entry.Id,
            golfer.FullName,
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
