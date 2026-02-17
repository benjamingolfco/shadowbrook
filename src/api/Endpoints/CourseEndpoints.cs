using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Endpoints;

public static class CourseEndpoints
{
    public static void MapCourseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/courses");

        group.MapPost("", CreateCourse);
        group.MapGet("", GetAllCourses);
        group.MapGet("{id:guid}", GetCourseById);
        group.MapPut("{id:guid}/tee-time-settings", UpdateTeeTimeSettings);
        group.MapGet("{id:guid}/tee-time-settings", GetTeeTimeSettings);
        group.MapPut("{id:guid}/pricing", UpdatePricing);
        group.MapGet("{id:guid}/pricing", GetPricing);
    }

    private static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        // Validate that the tenant exists
        var tenantExists = await db.Tenants.AnyAsync(t => t.Id == request.TenantId);
        if (!tenantExists)
            return Results.BadRequest(new { error = "Tenant does not exist." });

        var course = new Course
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Name = request.Name,
            StreetAddress = request.StreetAddress,
            City = request.City,
            State = request.State,
            ZipCode = request.ZipCode,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Courses.Add(course);
        await db.SaveChangesAsync();

        return Results.Created($"/courses/{course.Id}", course);
    }

    private static async Task<IResult> GetAllCourses(ApplicationDbContext db)
    {
        var courses = await db.Courses.ToListAsync();
        return Results.Ok(courses);
    }

    private static async Task<IResult> GetCourseById(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FindAsync(id);
        return course is null ? Results.NotFound() : Results.Ok(course);
    }

    private static readonly int[] AllowedIntervals = [8, 10, 12];

    private static async Task<IResult> UpdateTeeTimeSettings(
        Guid id,
        TeeTimeSettingsRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FindAsync(id);
        if (course is null)
            return Results.NotFound();

        if (!AllowedIntervals.Contains(request.TeeTimeIntervalMinutes))
            return Results.BadRequest(new { error = "Interval must be 8, 10, or 12 minutes." });

        if (request.FirstTeeTime >= request.LastTeeTime)
            return Results.BadRequest(new { error = "First tee time must be before last tee time." });

        course.TeeTimeIntervalMinutes = request.TeeTimeIntervalMinutes;
        course.FirstTeeTime = request.FirstTeeTime;
        course.LastTeeTime = request.LastTeeTime;
        course.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes.Value,
            course.FirstTeeTime.Value,
            course.LastTeeTime.Value));
    }

    private static async Task<IResult> GetTeeTimeSettings(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FindAsync(id);
        if (course is null)
            return Results.NotFound();

        if (course.TeeTimeIntervalMinutes is null || course.FirstTeeTime is null || course.LastTeeTime is null)
            return Results.Ok(new { });

        return Results.Ok(new TeeTimeSettingsResponse(
            course.TeeTimeIntervalMinutes.Value,
            course.FirstTeeTime.Value,
            course.LastTeeTime.Value));
    }

    private static async Task<IResult> UpdatePricing(
        Guid id,
        PricingRequest request,
        ApplicationDbContext db)
    {
        var course = await db.Courses.FindAsync(id);
        if (course is null)
            return Results.NotFound();

        if (request.FlatRatePrice < 0)
            return Results.BadRequest(new { error = "Price must be greater than or equal to 0." });

        if (request.FlatRatePrice > 10000)
            return Results.BadRequest(new { error = "Price must be less than or equal to 10000." });

        course.FlatRatePrice = request.FlatRatePrice;
        course.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return Results.Ok(new PricingResponse(course.FlatRatePrice.Value));
    }

    private static async Task<IResult> GetPricing(Guid id, ApplicationDbContext db)
    {
        var course = await db.Courses.FindAsync(id);
        if (course is null)
            return Results.NotFound();

        if (course.FlatRatePrice is null)
            return Results.Ok(new { });

        return Results.Ok(new PricingResponse(course.FlatRatePrice.Value));
    }
}

public record CreateCourseRequest(
    Guid TenantId,
    string Name,
    string? StreetAddress = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? ContactEmail = null,
    string? ContactPhone = null);

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
