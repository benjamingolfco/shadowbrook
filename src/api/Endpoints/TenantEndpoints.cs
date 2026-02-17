using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Models;
using System.Text.RegularExpressions;

namespace Shadowbrook.Api.Endpoints;

public static partial class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tenants");

        group.MapPost("", CreateTenant);
        group.MapGet("", GetAllTenants);
        group.MapGet("{id:guid}", GetTenantById);
    }

    private static async Task<IResult> CreateTenant(
        CreateTenantRequest request,
        ApplicationDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.OrganizationName))
            return Results.BadRequest(new { error = "OrganizationName is required." });

        if (string.IsNullOrWhiteSpace(request.ContactName))
            return Results.BadRequest(new { error = "ContactName is required." });

        if (string.IsNullOrWhiteSpace(request.ContactEmail))
            return Results.BadRequest(new { error = "ContactEmail is required." });

        if (!IsValidEmail(request.ContactEmail))
            return Results.BadRequest(new { error = "ContactEmail must be a valid email address." });

        if (string.IsNullOrWhiteSpace(request.ContactPhone))
            return Results.BadRequest(new { error = "ContactPhone is required." });

        // Check for duplicate organization name (case-insensitive via column collation)
        var existingTenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationName == request.OrganizationName);

        if (existingTenant is not null)
            return Results.Conflict(new { error = "A tenant with this organization name already exists." });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationName = request.OrganizationName,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var response = new TenantResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.CreatedAt,
            tenant.UpdatedAt);

        return Results.Created($"/tenants/{tenant.Id}", response);
    }

    private static async Task<IResult> GetAllTenants(ApplicationDbContext db)
    {
        var tenants = await db.Tenants
            .Select(t => new TenantListResponse(
                t.Id,
                t.OrganizationName,
                t.ContactName,
                t.ContactEmail,
                t.ContactPhone,
                t.Courses.Count,
                t.CreatedAt,
                t.UpdatedAt))
            .ToListAsync();

        return Results.Ok(tenants);
    }

    private static async Task<IResult> GetTenantById(Guid id, ApplicationDbContext db)
    {
        var tenant = await db.Tenants
            .Include(t => t.Courses)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null)
            return Results.NotFound();

        var response = new TenantDetailResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.Courses.Select(c => new CourseInfo(c.Id, c.Name)).ToList(),
            tenant.CreatedAt,
            tenant.UpdatedAt);

        return Results.Ok(response);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();

    private static bool IsValidEmail(string email)
    {
        return EmailRegex().IsMatch(email);
    }
}

public record CreateTenantRequest(
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone);

public record TenantResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TenantListResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    int CourseCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record TenantDetailResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    List<CourseInfo> Courses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CourseInfo(Guid Id, string Name);
