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

public record DraftTeeSheetRequest(DateOnly Date);

public class DraftTeeSheetRequestValidator : AbstractValidator<DraftTeeSheetRequest>
{
    public DraftTeeSheetRequestValidator()
    {
        RuleFor(r => r.Date).NotEmpty();
    }
}

public static class DraftTeeSheetEndpoint
{
    [WolverinePost("/courses/{courseId}/tee-sheets/draft")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> Handle(
        Guid courseId,
        DraftTeeSheetRequest request,
        ICourseRepository courseRepository,
        ITeeSheetRepository teeSheetRepository,
        ITimeProvider timeProvider,
        CancellationToken ct)
    {
        var course = await courseRepository.GetRequiredByIdAsync(courseId);

        var existing = await teeSheetRepository.GetByCourseAndDateAsync(courseId, request.Date, ct);
        if (existing is not null)
        {
            throw new TeeSheetAlreadyExistsException(courseId, request.Date);
        }

        var settings = course.CurrentScheduleDefaults();
        var sheet = TeeSheetAggregate.Draft(courseId, request.Date, settings, timeProvider);
        teeSheetRepository.Add(sheet);

        return Results.Ok(new { teeSheetId = sheet.Id });
    }
}
