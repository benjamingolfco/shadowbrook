using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.TenantAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Courses;

public static class CourseEndpoints
{
    [WolverinePost("/courses")]
    public static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        [NotBody] ICourseRepository courseRepository,
        [NotBody] ITenantRepository tenantRepository,
        [NotBody] ICurrentUser currentUser)
    {
        var tenantId = currentUser.TenantId ?? request.TenantId;
        if (tenantId is null)
        {
            return Results.BadRequest(new { error = "TenantId is required (via X-Tenant-Id header or request body)." });
        }

        var tenant = await tenantRepository.GetByIdAsync(tenantId.Value);
        if (tenant is null)
        {
            return Results.BadRequest(new { error = "Tenant does not exist." });
        }

        var duplicateExists = await courseRepository.ExistsByNameAsync(tenantId.Value, request.Name);
        if (duplicateExists)
        {
            return Results.Conflict(new { error = "A course with this name already exists for this tenant." });
        }

        var course = Course.Create(tenantId.Value, request.Name, request.TimeZoneId,
            request.StreetAddress, request.City, request.State, request.ZipCode,
            request.ContactEmail, request.ContactPhone);

        courseRepository.Add(course);

        var response = new CourseResponse(
            course.Id,
            course.Name,
            course.StreetAddress,
            course.City,
            course.State,
            course.ZipCode,
            course.ContactEmail,
            course.ContactPhone,
            course.TimeZoneId,
            course.CreatedAt,
            new TenantInfo(tenant.Id, tenant.OrganizationName));

        return Results.Created($"/courses/{course.Id}", response);
    }

    [WolverineGet("/courses")]
    public static async Task<IResult> GetAllCourses(ApplicationDbContext db)
    {
        var courses = await (
            from c in db.Courses
            join t in db.Tenants on c.OrganizationId equals t.Id
            select new CourseResponse(
                c.Id,
                c.Name,
                c.StreetAddress,
                c.City,
                c.State,
                c.ZipCode,
                c.ContactEmail,
                c.ContactPhone,
                c.TimeZoneId,
                c.CreatedAt,
                new TenantInfo(t.Id, t.OrganizationName)))
            .ToListAsync();

        return Results.Ok(courses);
    }

    [WolverineGet("/courses/{id}")]
    public static async Task<IResult> GetCourseById(Guid id, ApplicationDbContext db)
    {
        var course = await (
            from c in db.Courses
            join t in db.Tenants on c.OrganizationId equals t.Id
            where c.Id == id
            select new CourseResponse(
                c.Id,
                c.Name,
                c.StreetAddress,
                c.City,
                c.State,
                c.ZipCode,
                c.ContactEmail,
                c.ContactPhone,
                c.TimeZoneId,
                c.CreatedAt,
                new TenantInfo(t.Id, t.OrganizationName)))
            .FirstOrDefaultAsync();

        return course is null ? Results.NotFound() : Results.Ok(course);
    }

    [WolverinePut("/courses/{id}/tee-time-settings")]
    public static async Task<IResult> UpdateTeeTimeSettings(
        Guid id,
        TeeTimeSettingsRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);

        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        course.UpdateTeeTimeSettings(request.TeeTimeIntervalMinutes, request.FirstTeeTime, request.LastTeeTime);

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes!.Value,
            course.FirstTeeTime!.Value,
            course.LastTeeTime!.Value));
    }

    [WolverineGet("/courses/{id}/tee-time-settings")]
    public static async Task<IResult> GetTeeTimeSettings(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);

        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
        {
            return Results.Ok(new { });
        }

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes.Value,
            course.FirstTeeTime.Value,
            course.LastTeeTime.Value));
    }

    [WolverinePut("/courses/{id}/pricing")]
    public static async Task<IResult> UpdatePricing(
        Guid id,
        PricingRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);

        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        course.UpdatePricing(request.FlatRatePrice);

        return Results.Ok(new PricingResponse(course.FlatRatePrice!.Value));
    }

    [WolverineGet("/courses/{id}/pricing")]
    public static async Task<IResult> GetPricing(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);

        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        if (course.FlatRatePrice is null)
        {
            return Results.Ok(new { });
        }

        return Results.Ok(new PricingResponse(course.FlatRatePrice.Value));
    }
}

public record CreateCourseRequest(
    string Name,
    string TimeZoneId,
    Guid? TenantId = null,
    string? StreetAddress = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? ContactEmail = null,
    string? ContactPhone = null);

public record CourseResponse(
    Guid Id,
    string Name,
    string? StreetAddress,
    string? City,
    string? State,
    string? ZipCode,
    string? ContactEmail,
    string? ContactPhone,
    string TimeZoneId,
    DateTimeOffset CreatedAt,
    TenantInfo Tenant);

public record TenantInfo(Guid Id, string OrganizationName);

public record TeeTimeSettingsRequest(
    int TeeTimeIntervalMinutes,
    TimeOnly FirstTeeTime,
    TimeOnly LastTeeTime);

public record TeeTimeSettingsResponse(
    int TeeTimeIntervalMinutes,
    TimeOnly FirstTeeTime,
    TimeOnly LastTeeTime);

public record PricingRequest(decimal FlatRatePrice);

public record PricingResponse(decimal FlatRatePrice);

public class CreateCourseRequestValidator : AbstractValidator<CreateCourseRequest>
{
    public CreateCourseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.TimeZoneId)
            .NotEmpty().WithMessage("TimeZoneId is required.");
        RuleFor(x => x.TimeZoneId)
            .Must(id =>
            {
                try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
                catch { return false; }
            })
            .When(x => !string.IsNullOrEmpty(x.TimeZoneId))
            .WithMessage("TimeZoneId is not a valid IANA timezone.");
    }
}

public class PricingRequestValidator : AbstractValidator<PricingRequest>
{
    public PricingRequestValidator()
    {
        RuleFor(x => x.FlatRatePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0.")
            .LessThanOrEqualTo(10000).WithMessage("Price must be less than or equal to 10000.");
    }
}

public class TeeTimeSettingsRequestValidator : AbstractValidator<TeeTimeSettingsRequest>
{
    private static readonly int[] allowedIntervals = [8, 10, 12];

    public TeeTimeSettingsRequestValidator()
    {
        RuleFor(x => x.TeeTimeIntervalMinutes)
            .Must(i => allowedIntervals.Contains(i))
            .WithMessage("Interval must be 8, 10, or 12 minutes.");
        RuleFor(x => x.FirstTeeTime)
            .LessThan(x => x.LastTeeTime)
            .WithMessage("First tee time must be before last tee time.");
    }
}
