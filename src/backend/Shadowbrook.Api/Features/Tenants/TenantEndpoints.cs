using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.TenantAggregate;
using Wolverine.Http;

namespace Shadowbrook.Api.Features.Tenants;

public static class TenantEndpoints
{
    [WolverinePost("/tenants")]
    public static async Task<IResult> CreateTenant(
        CreateTenantRequest request,
        [NotBody] ITenantRepository tenantRepository)
    {
        var exists = await tenantRepository.ExistsByNameAsync(request.OrganizationName);
        if (exists)
        {
            return Results.Conflict(new { error = "A tenant with this organization name already exists." });
        }

        var tenant = Tenant.Create(request.OrganizationName, request.ContactName, request.ContactEmail, request.ContactPhone);
        tenantRepository.Add(tenant);

        var response = new TenantResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            tenant.CreatedAt);

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
                db.Courses.IgnoreQueryFilters().Count(c => c.OrganizationId == t.Id),
                t.CreatedAt))
            .ToListAsync();

        return Results.Ok(tenants);
    }

    [WolverineGet("/tenants/{id}")]
    public static async Task<IResult> GetTenantById(
        Guid id,
        [NotBody] ITenantRepository tenantRepository,
        [NotBody] ICourseRepository courseRepository)
    {
        var tenant = await tenantRepository.GetByIdAsync(id);
        if (tenant is null)
        {
            return Results.NotFound();
        }

        var courses = await courseRepository.GetByTenantIdAsync(id);

        var response = new TenantDetailResponse(
            tenant.Id,
            tenant.OrganizationName,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.ContactPhone,
            courses.Select(c => new CourseInfo(c.Id, c.Name, c.City, c.State)).ToList(),
            tenant.CreatedAt);

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
    DateTimeOffset CreatedAt);

public record TenantListResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    int CourseCount,
    DateTimeOffset CreatedAt);

public record TenantDetailResponse(
    Guid Id,
    string OrganizationName,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    List<CourseInfo> Courses,
    DateTimeOffset CreatedAt);

public record CourseInfo(Guid Id, string Name, string? City, string? State);
