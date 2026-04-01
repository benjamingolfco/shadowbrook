using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Shadowbrook.Api.Infrastructure.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddShadowbrookAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Authentication
        var useDevAuth = configuration.GetValue<bool>("Auth:UseDevAuth");
        if (useDevAuth)
        {
            services.AddAuthentication("DevAuth")
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", null);
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(
                    jwtBearerOptions => { },
                    microsoftIdentityOptions => configuration.GetSection("AzureAd").Bind(microsoftIdentityOptions));
        }

        // Scope validation — applied to all policies when not in dev mode
        var requiredScopes = configuration
            .GetValue<string>("AzureAd:Scopes")?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        // Authorization policies
        services.AddAuthorizationBuilder()
            .AddPolicy("RequireAppAccess", policy =>
            {
                policy.AddRequirements(new PermissionRequirement(Permissions.AppAccess));
                if (!useDevAuth && requiredScopes.Length > 0)
                {
                    policy.AddRequirements(new ScopeRequirement(requiredScopes));
                }
            })
            .AddPolicy("RequireUsersManage", policy =>
            {
                policy.AddRequirements(new PermissionRequirement(Permissions.UsersManage));
                if (!useDevAuth && requiredScopes.Length > 0)
                {
                    policy.AddRequirements(new ScopeRequirement(requiredScopes));
                }
            })
            .AddPolicy("RequireCourseAccess", policy =>
            {
                policy.AddRequirements(new CourseAccessRequirement());
                if (!useDevAuth && requiredScopes.Length > 0)
                {
                    policy.AddRequirements(new ScopeRequirement(requiredScopes));
                }
            });

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, CourseAccessAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ScopeAuthorizationHandler>();

        return services;
    }
}
