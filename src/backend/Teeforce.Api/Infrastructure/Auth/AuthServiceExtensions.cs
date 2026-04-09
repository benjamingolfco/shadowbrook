using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Identity.Web;
using Teeforce.Api.Infrastructure.Configuration;

namespace Teeforce.Api.Infrastructure.Auth;

public static class AuthServiceExtensions
{
    public static IServiceCollection AddTeeforceAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        var authSection = configuration.GetSection(AuthSettings.SectionName);
        services.Configure<AuthSettings>(authSection);
        var authSettings = authSection.Get<AuthSettings>()!;

        if (authSettings.UseDevAuth)
        {
            services.AddAuthentication("DevAuth")
                .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", null);
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));
        }

        services.AddScoped<IClaimsTransformation, AppUserClaimsTransformation>();

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireAppUser, policy =>
                policy.AddRequirements(new RequireAppUserRequirement()))
            .AddPolicy(AuthorizationPolicies.RequireAppAccess, policy =>
                policy.AddRequirements(new RequireAppUserRequirement(), new PermissionRequirement(Permissions.AppAccess)))
            .AddPolicy(AuthorizationPolicies.RequireUsersManage, policy =>
                policy.AddRequirements(new RequireAppUserRequirement(), new PermissionRequirement(Permissions.UsersManage)))
            .AddPolicy(AuthorizationPolicies.RequireAppAccessOrOfferToken, policy =>
                policy.AddRequirements(new AppAccessOrOfferTokenRequirement()));

        services.AddScoped<IAuthorizationHandler, RequireAppUserHandler>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, TeeTimeOfferTokenAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AppAccessOrOfferTokenPermissionHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler>(sp =>
            new AppUserAuthorizationResultHandler(new AuthorizationMiddlewareResultHandler()));

        return services;
    }
}
