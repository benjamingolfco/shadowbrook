using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.TeeSheetAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public static class BookingCountEndpoint
{
    [WolverineGet("/courses/{courseId}/tee-sheets/{date}/booking-count")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        DateOnly date,
        ITeeSheetRepository teeSheetRepository,
        IBookingRepository bookingRepository,
        CancellationToken ct)
    {
        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId, date, ct);
        if (sheet is null)
        {
            return Results.Ok(new { count = 0 });
        }

        var count = await bookingRepository.GetConfirmedCountByTeeSheetIdAsync(sheet.Id, ct);
        return Results.Ok(new { count });
    }
}
