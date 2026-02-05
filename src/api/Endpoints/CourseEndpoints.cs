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
    }

    private static async Task<IResult> CreateCourse(
        CreateCourseRequest request,
        ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });

        var course = new Course
        {
            Id = Guid.NewGuid(),
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
}

public record CreateCourseRequest(
    string Name,
    string? StreetAddress = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? ContactEmail = null,
    string? ContactPhone = null);
