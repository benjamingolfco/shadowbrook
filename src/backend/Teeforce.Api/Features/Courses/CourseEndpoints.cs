using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.OrganizationAggregate;
using Teeforce.Domain.TenantAggregate;
using Wolverine.Http;

namespace Teeforce.Api.Features.Courses;

public static class CourseEndpoints
{
    [WolverinePost("/courses")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        [NotBody] ICourseRepository courseRepository,
        [NotBody] ITenantRepository tenantRepository,
        [NotBody] ApplicationDbContext db,
        [NotBody] IUserContext userContext)
    {
        var organizationId = userContext.OrganizationId ?? request.OrganizationId;
        if (organizationId is null)
        {
            return Results.BadRequest(new { error = "OrganizationId is required." });
        }

        // Look up Organization first (admin-created orgs only exist here).
        // Fall back to Tenant for legacy tenants — create an Organization row if one is missing.
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId.Value);
        string orgName;

        if (org is not null)
        {
            orgName = org.Name;
        }
        else
        {
            var tenant = await tenantRepository.GetByIdAsync(organizationId.Value);
            if (tenant is null)
            {
                return Results.BadRequest(new { error = "Organization does not exist." });
            }

            orgName = tenant.OrganizationName;
            var bridgeOrg = Organization.CreateWithId(organizationId.Value, orgName);
            db.Organizations.Add(bridgeOrg);
        }

        var duplicateExists = await courseRepository.ExistsByNameAsync(organizationId.Value, request.Name);
        if (duplicateExists)
        {
            return Results.Conflict(new { error = "A course with this name already exists for this tenant." });
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
            new TenantInfo(organizationId.Value, orgName));

        return Results.Created($"/courses/{course.Id}", response);
    }

    [WolverineGet("/courses")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetAllCourses(ApplicationDbContext db)
    {
        var courses = await (
            from c in db.Courses
            join o in db.Organizations on c.OrganizationId equals o.Id into orgs
            from o in orgs.DefaultIfEmpty()
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
                o == null ? null : new TenantInfo(o.Id, o.Name)))
            .ToListAsync();

        return Results.Ok(courses);
    }

    [WolverineGet("/courses/{courseId}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
    public static async Task<IResult> GetCourseById(Guid courseId, ApplicationDbContext db)
    {
        var course = await (
            from c in db.Courses
            join o in db.Organizations on c.OrganizationId equals o.Id into orgs
            from o in orgs.DefaultIfEmpty()
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
                o == null ? null : new TenantInfo(o.Id, o.Name)))
            .FirstOrDefaultAsync();

        return course is null ? Results.NotFound() : Results.Ok(course);
    }

    [WolverinePut("/courses/{courseId}")]
    [Authorize(Policy = AuthorizationPolicies.RequireUsersManage)]
    public static async Task<IResult> UpdateCourse(
        Guid courseId,
        UpdateCourseRequest request,
        [NotBody] ApplicationDbContext db)
    {
        var course = await db.Courses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == courseId);

        if (course is null)
        {
            return Results.NotFound();
        }

        course.UpdateDetails(request.Name, request.TimeZoneId);

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == course.OrganizationId);

        return Results.Ok(new CourseResponse(
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
            org is null ? null : new TenantInfo(org.Id, org.Name)));
    }

    [WolverinePut("/courses/{courseId}/tee-time-settings")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
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
        course.UpdateDefaultCapacity(request.DefaultCapacity);

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes!.Value,
            course.FirstTeeTime!.Value,
            course.LastTeeTime!.Value,
            course.DefaultCapacity));
    }

    [WolverineGet("/courses/{courseId}/tee-time-settings")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
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
            course.LastTeeTime.Value,
            course.DefaultCapacity));
    }

    [WolverinePut("/courses/{courseId}/pricing")]
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
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
    [Authorize(Policy = AuthorizationPolicies.RequireAppAccess)]
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

public sealed record UpdateCourseRequest(string Name, string TimeZoneId);

public record TeeTimeSettingsRequest(
    int TeeTimeIntervalMinutes,
    TimeOnly FirstTeeTime,
    TimeOnly LastTeeTime,
    int DefaultCapacity);

public record TeeTimeSettingsResponse(
    int TeeTimeIntervalMinutes,
    TimeOnly FirstTeeTime,
    TimeOnly LastTeeTime,
    int DefaultCapacity);

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

public class UpdateCourseRequestValidator : AbstractValidator<UpdateCourseRequest>
{
    public UpdateCourseRequestValidator()
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
    public TeeTimeSettingsRequestValidator()
    {
        RuleFor(x => x.TeeTimeIntervalMinutes)
            .GreaterThan(0)
            .WithMessage("Interval must be greater than 0.");
        RuleFor(x => x.FirstTeeTime)
            .LessThan(x => x.LastTeeTime)
            .WithMessage("First tee time must be before last tee time.");
        RuleFor(x => x.DefaultCapacity)
            .GreaterThan(0)
            .WithMessage("Default capacity must be greater than 0.");
    }
}
