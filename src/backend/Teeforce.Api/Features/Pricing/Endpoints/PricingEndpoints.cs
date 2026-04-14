using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Domain.CoursePricingAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.Pricing.Endpoints;

public static class PricingEndpoints
{
    [WolverineGet("/courses/{courseId}/pricing")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetPricing(
        Guid courseId,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            return Results.Ok(new GetPricingResponse(null, null, null, []));
        }

        var schedules = settings.RateSchedules.Select(s => new RateScheduleResponse(
            s.Id, s.Name, s.DaysOfWeek, s.StartTime, s.EndTime, s.Price, s.InvalidReason)).ToList();

        return Results.Ok(new GetPricingResponse(settings.DefaultPrice, settings.MinPrice, settings.MaxPrice, schedules));
    }

    [WolverinePut("/courses/{courseId}/pricing/default")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> UpdateDefaultPrice(
        Guid courseId,
        UpdateDefaultPriceRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettings(courseId, pricingRepository, ct);
        settings.UpdateDefaultPrice(request.DefaultPrice);
        return Results.Ok(new { defaultPrice = settings.DefaultPrice });
    }

    [WolverinePut("/courses/{courseId}/pricing/bounds")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> UpdateBounds(
        Guid courseId,
        UpdateBoundsRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettings(courseId, pricingRepository, ct);
        settings.UpdateBounds(request.MinPrice, request.MaxPrice);
        return Results.Ok(new { minPrice = settings.MinPrice, maxPrice = settings.MaxPrice });
    }

    [WolverinePost("/courses/{courseId}/pricing/schedules")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> CreateSchedule(
        Guid courseId,
        CreateScheduleRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettings(courseId, pricingRepository, ct);
        var schedule = settings.AddSchedule(request.Name, request.DaysOfWeek, request.StartTime, request.EndTime, request.Price);

        return Results.Created(
            $"/courses/{courseId}/pricing/schedules/{schedule.Id}",
            new RateScheduleResponse(schedule.Id, schedule.Name, schedule.DaysOfWeek, schedule.StartTime, schedule.EndTime, schedule.Price, schedule.InvalidReason));
    }

    [WolverinePut("/courses/{courseId}/pricing/schedules/{scheduleId}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> UpdateSchedule(
        Guid courseId,
        Guid scheduleId,
        UpdateScheduleRequest request,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            return Results.NotFound(new { error = "Pricing settings not found." });
        }

        settings.UpdateSchedule(scheduleId, request.Name, request.DaysOfWeek, request.StartTime, request.EndTime, request.Price);

        var updated = settings.RateSchedules.First(s => s.Id == scheduleId);
        return Results.Ok(new RateScheduleResponse(updated.Id, updated.Name, updated.DaysOfWeek, updated.StartTime, updated.EndTime, updated.Price, updated.InvalidReason));
    }

    [WolverineDelete("/courses/{courseId}/pricing/schedules/{scheduleId}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> DeleteSchedule(
        Guid courseId,
        Guid scheduleId,
        ICoursePricingSettingsRepository pricingRepository,
        CancellationToken ct)
    {
        var settings = await pricingRepository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            return Results.NotFound(new { error = "Pricing settings not found." });
        }

        settings.RemoveSchedule(scheduleId);
        return Results.NoContent();
    }

    private static async Task<CoursePricingSettings> GetOrCreateSettings(
        Guid courseId,
        ICoursePricingSettingsRepository repository,
        CancellationToken ct)
    {
        var settings = await repository.GetByCourseIdAsync(courseId, ct);
        if (settings is null)
        {
            settings = CoursePricingSettings.Create(courseId);
            repository.Add(settings);
        }

        return settings;
    }
}

public record GetPricingResponse(
    decimal? DefaultPrice,
    decimal? MinPrice,
    decimal? MaxPrice,
    List<RateScheduleResponse> Schedules);

public record RateScheduleResponse(
    Guid Id,
    string Name,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price,
    string? InvalidReason);

public record UpdateDefaultPriceRequest(decimal? DefaultPrice);

public record UpdateBoundsRequest(decimal? MinPrice, decimal? MaxPrice);

public record CreateScheduleRequest(
    string Name,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price);

public record UpdateScheduleRequest(
    string Name,
    DayOfWeek[] DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Price);

public class CreateScheduleRequestValidator : AbstractValidator<CreateScheduleRequest>
{
    public CreateScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DaysOfWeek).NotEmpty().WithMessage("At least one day of week is required.");
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime).WithMessage("Start time must be before end time.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

public class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DaysOfWeek).NotEmpty().WithMessage("At least one day of week is required.");
        RuleFor(x => x.StartTime).LessThan(x => x.EndTime).WithMessage("Start time must be before end time.");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than zero.");
    }
}

public class UpdateBoundsRequestValidator : AbstractValidator<UpdateBoundsRequest>
{
    public UpdateBoundsRequestValidator()
    {
        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Min price must be non-negative.")
            .When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice)
            .GreaterThan(0).WithMessage("Max price must be greater than zero.")
            .When(x => x.MaxPrice is not null);
        RuleFor(x => x)
            .Must(x => x.MinPrice is null || x.MaxPrice is null || x.MinPrice <= x.MaxPrice)
            .WithMessage("Min price must be less than or equal to max price.");
    }
}
