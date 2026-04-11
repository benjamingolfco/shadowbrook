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

public record BulkDraftItem(DateOnly Date, Guid TeeSheetId);

public record BulkDraftResponse(List<BulkDraftItem> TeeSheets);

public class BulkDraftRequestValidator : AbstractValidator<BulkDraftRequest>
{
    public BulkDraftRequestValidator()
    {
        RuleFor(r => r.Dates).NotEmpty();
        RuleFor(r => r.Dates)
            .Must(dates => dates.Distinct().Count() == dates.Count)
            .WithMessage("Dates must not contain duplicates.");
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

        var existingSheets = await teeSheetRepository.GetByCourseAndDatesAsync(courseId, request.Dates, ct);
        if (existingSheets.Count > 0)
        {
            throw new TeeSheetAlreadyExistsException(courseId, existingSheets[0].Date);
        }

        var results = new List<BulkDraftItem>();

        foreach (var date in request.Dates)
        {
            var sheet = TeeSheetAggregate.Draft(courseId, date, settings, timeProvider);
            teeSheetRepository.Add(sheet);

            results.Add(new BulkDraftItem(date, sheet.Id));
        }

        return Results.Ok(new BulkDraftResponse(results));
    }
}
