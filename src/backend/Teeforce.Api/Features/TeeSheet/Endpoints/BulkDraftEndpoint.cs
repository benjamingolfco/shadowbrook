using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeSheetAggregate.Exceptions;
using Wolverine.Http;
using TeeSheetAggregate = Teeforce.Domain.TeeSheetAggregate.TeeSheet;

namespace Teeforce.Api.Features.TeeSheet.Endpoints;

public record BulkDraftRequest(List<DateOnly> Dates);

public record BulkDraftItem(string Date, Guid TeeSheetId);

public record BulkDraftResponse(List<BulkDraftItem> TeeSheets);

public class BulkDraftRequestValidator : AbstractValidator<BulkDraftRequest>
{
    public BulkDraftRequestValidator()
    {
        RuleFor(r => r.Dates).NotEmpty();
    }
}

public static class BulkDraftEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-sheets/draft")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        BulkDraftRequest request,
        ICourseRepository courseRepository,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var course = await courseRepository.GetRequiredByIdAsync(courseId);
        var settings = course.CurrentScheduleDefaults();

        var results = new List<BulkDraftItem>();

        foreach (var date in request.Dates)
        {
            var existing = await teeSheetRepository.GetByCourseAndDateAsync(courseId, date, ct);
            if (existing is not null)
            {
                throw new TeeSheetAlreadyExistsException(courseId, date);
            }

            var sheet = TeeSheetAggregate.Draft(courseId, date, settings, timeProvider);
            teeSheetRepository.Add(sheet);

            results.Add(new BulkDraftItem(date.ToString("yyyy-MM-dd"), sheet.Id));
        }

        return Results.Ok(new BulkDraftResponse(results));
    }
}
