using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Wolverine.Http;
using TeeTimeAggregate = Teeforce.Domain.TeeTimeAggregate.TeeTime;

namespace Teeforce.Api.Features.Bookings.Endpoints;

public record BookTeeTimeRequest(
    Guid BookingId,
    Guid TeeSheetIntervalId,
    Guid GolferId,
    int GroupSize);

public class BookTeeTimeRequestValidator : AbstractValidator<BookTeeTimeRequest>
{
    public BookTeeTimeRequestValidator()
    {
        RuleFor(r => r.BookingId).NotEmpty();
        RuleFor(r => r.TeeSheetIntervalId).NotEmpty();
        RuleFor(r => r.GolferId).NotEmpty();
        RuleFor(r => r.GroupSize).GreaterThan(0);
    }
}

public static class BookTeeTimeEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-times/book")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        BookTeeTimeRequest request,
        ITeeSheetRepository teeSheetRepository,
        ITeeTimeRepository teeTimeRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var sheet = await teeSheetRepository.GetByIntervalIdAsync(request.TeeSheetIntervalId, ct);
        if (sheet is null)
        {
            return Results.NotFound(new { error = "Tee sheet interval not found." });
        }

        var auth = sheet.AuthorizeBooking(); // throws TeeSheetNotPublishedException if Draft
        var interval = sheet.Intervals.Single(i => i.Id == request.TeeSheetIntervalId);

        var existing = await teeTimeRepository.GetByIntervalIdAsync(request.TeeSheetIntervalId, ct);
        if (existing is null)
        {
            var teeTime = TeeTimeAggregate.Claim(
                interval,
                courseId,
                sheet.Date,
                auth,
                request.BookingId,
                request.GolferId,
                request.GroupSize,
                timeProvider);
            teeTimeRepository.Add(teeTime);
        }
        else
        {
            existing.Claim(auth, request.BookingId, request.GolferId, request.GroupSize, timeProvider);
        }

        return Results.Ok(new { bookingId = request.BookingId });
    }
}
