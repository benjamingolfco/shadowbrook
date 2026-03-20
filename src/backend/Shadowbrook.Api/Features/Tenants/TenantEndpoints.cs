using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Models;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Tenants;

public static class TenantEndpoints
{
    [WolverinePost("/tenants")]
    public static async Task<IResult> CreateTenant(
        CreateTenantRequest request,
        ApplicationDbContext db)
    {
        // Check for duplicate organization name (case-insensitive)
        var normalizedName = request.OrganizationName.ToLower();
        var existingTenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationName.ToLower() == normalizedName);

        if (existingTenant is not null)
        {
            return Results.Conflict(new { error = "A tenant with this organization name already exists." });
        }

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

    [WolverineGet("/tenants")]
    public static async Task<IResult> GetAllTenants(ApplicationDbContext db)
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

    [WolverineGet("/tenants/{id}")]
    public static async Task<IResult> GetTenantById(Guid id, ApplicationDbContext db)
    {
        var tenant = await db.Tenants
            .Include(t => t.Courses)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tenant is null)
        {
            return Results.NotFound();
        }

        var response = new TenantDetailResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.Courses.Select(c => new CourseInfo(c.Id, c.Name, c.City, c.State)).ToList(),
            tenant.CreatedAt,
            tenant.UpdatedAt);

        return Results.Ok(response);
    }

}

public record CreateTenantRequest(
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone);

public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.OrganizationName).NotEmpty();
        RuleFor(x => x.ContactName).NotEmpty();
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactPhone).NotEmpty();
    }
}

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

public record CourseInfo(Guid Id, string Name, string? City, string? State);
