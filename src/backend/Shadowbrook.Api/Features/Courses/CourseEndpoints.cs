using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.OrganizationAggregate;
using Shadowbrook.Domain.TenantAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Courses;

public static class CourseEndpoints
{
    [WolverinePost("/courses")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        [NotBody] ICourseRepository courseRepository,
        [NotBody] ITenantRepository tenantRepository,
        [NotBody] ApplicationDbContext db,
        [NotBody] ICurrentUser currentUser)
    {
        var organizationId = currentUser.OrganizationId ?? request.OrganizationId;
        if (organizationId is null)
        {
            return Results.BadRequest(new { error = "OrganizationId is required." });
        }

        var tenant = await tenantRepository.GetByIdAsync(organizationId.Value);
        if (tenant is null)
        {
            return Results.BadRequest(new { error = "Tenant does not exist." });
        }

        var duplicateExists = await courseRepository.ExistsByNameAsync(organizationId.Value, request.Name);
        if (duplicateExists)
        {
            return Results.Conflict(new { error = "A course with this name already exists for this tenant." });
        }

        // Transitional bridge: ensure an Organization row exists with the same ID as the tenant.
        // Courses must reference Organizations. This will be replaced when the full org/auth flow lands.
        var organizationExists = await db.Organizations.AnyAsync(o => o.Id == organizationId.Value);
        if (!organizationExists)
        {
            var organization = Organization.CreateWithId(organizationId.Value, tenant.OrganizationName);
            db.Organizations.Add(organization);
        }

        var course = Course.Create(organizationId.Value, request.Name, request.TimeZoneId,
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
    [Authorize(Policy = "RequireAppAccess")]
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

    [WolverineGet("/courses/{courseId}")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> GetCourseById(Guid courseId, ApplicationDbContext db)
    {
        var course = await (
            from c in db.Courses
            join t in db.Tenants on c.OrganizationId equals t.Id into tenants
            from t in tenants.DefaultIfEmpty()
            where c.Id == courseId
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
                t == null ? null : new TenantInfo(t.Id, t.OrganizationName)))
            .FirstOrDefaultAsync();

        return course is null ? Results.NotFound() : Results.Ok(course);
    }

    [WolverinePut("/courses/{courseId}/tee-time-settings")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> UpdateTeeTimeSettings(
        Guid courseId,
        TeeTimeSettingsRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);

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

    [WolverineGet("/courses/{courseId}/tee-time-settings")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> GetTeeTimeSettings(Guid courseId, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);

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

    [WolverinePut("/courses/{courseId}/pricing")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> UpdatePricing(
        Guid courseId,
        PricingRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
        {
            return Results.NotFound(new { error = "Course not found." });
        }

        course.UpdatePricing(request.FlatRatePrice);

        return Results.Ok(new PricingResponse(course.FlatRatePrice!.Value));
    }

    [WolverineGet("/courses/{courseId}/pricing")]
    [Authorize(Policy = "RequireAppAccess")]
    public static async Task<IResult> GetPricing(Guid courseId, ApplicationDbContext db)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId);

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
    Guid? OrganizationId = null,
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
    TenantInfo? Tenant);

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
