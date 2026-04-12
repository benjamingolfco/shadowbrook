using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.TeeSheetAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public record UnpublishTeeSheetRequest(string? Reason);

public static class UnpublishTeeSheetEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-sheets/{date}/unpublish")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        DateOnly date,
        UnpublishTeeSheetRequest request,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var sheet = await teeSheetRepository.GetByCourseAndDateAsync(courseId, date, ct);
        if (sheet is null)
        {
            return Results.NotFound(new { error = "Tee sheet not found." });
        }

        sheet.Unpublish(request.Reason, timeProvider);
        return Results.Ok(new { teeSheetId = sheet.Id, status = sheet.Status.ToString() });
    }
}
