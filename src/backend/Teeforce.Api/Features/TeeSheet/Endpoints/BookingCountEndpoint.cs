using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
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
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId, date, ct);
        if (sheet is null)
        {
            return Results.Ok(new { count = 0 });
        }

        var count = await db.Bookings
            .Where(b => b.TeeTimeId != null
                && db.TeeTimes.Where(t => t.TeeSheetId == sheet.Id).Select(t => t.Id).Contains(b.TeeTimeId.Value)
                && b.Status == BookingStatus.Confirmed)
            .CountAsync(ct);
        return Results.Ok(new { count });
    }
}
