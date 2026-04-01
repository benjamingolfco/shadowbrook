using Azure.Monitor.OpenTelemetry.AspNetCore;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Shadowbrook.Api.Features.Dev;
using Shadowbrook.Api.Infrastructure.Auth;
using Shadowbrook.Api.Infrastructure.Configuration;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Middleware;
using Shadowbrook.Api.Infrastructure.Observability;
using Shadowbrook.Api.Infrastructure.Repositories;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TenantAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
using Wolverine.SqlServer;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.With(services.GetRequiredService<OrganizationIdEnricher>())
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"),
    writeToProviders: true);

builder.Services.AddSingleton<OrganizationIdEnricher>();

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("Wolverine"))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation())
        .WithLogging()
        .UseAzureMonitor(o => o.ConnectionString = appInsightsConnectionString);
}

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000"];
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }));


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    if (connectionString is not null)
    {
        // Increase command timeout for Wolverine transport provisioning — Azure SQL Basic tier
        // (5 DTU) is too slow for the default 30s timeout during schema migration.
        var wolverineConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 120,
            CommandTimeout = 120
        }.ConnectionString;

#pragma warning disable CS0618 // EnableMessageTransport not implemented in 5.20.1
        opts.UseSqlServerPersistenceAndTransport(wolverineConnectionString, "wolverine")
            .AutoProvision();
#pragma warning restore CS0618
    }

    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

    opts.OnException<DbUpdateConcurrencyException>()
        .RetryTimes(3);

    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();
    opts.UseFluentValidation();
    opts.PublishDomainEventsFromEntityFrameworkCore<Entity, IDomainEvent>(e => e.DomainEvents);
});

builder.Services.AddSingleton<InMemoryTextMessageService>();
builder.Services.AddScoped<DatabaseTextMessageService>();
builder.Services.AddScoped<ITextMessageService>(sp => sp.GetRequiredService<DatabaseTextMessageService>());
builder.Services.AddSingleton<IFeatureService, FeatureService>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IGolferRepository, GolferRepository>();
builder.Services.AddScoped<ICourseWaitlistRepository, CourseWaitlistRepository>();
builder.Services.AddScoped<ITeeTimeOpeningRepository, TeeTimeOpeningRepository>();
builder.Services.AddScoped<Shadowbrook.Domain.WaitlistServices.WaitlistMatchingService>();
builder.Services.AddScoped<Shadowbrook.Domain.WaitlistServices.WaitlistOfferClaimService>();
builder.Services.AddScoped<IWaitlistOfferRepository, WaitlistOfferRepository>();
builder.Services.AddScoped<IGolferWaitlistEntryRepository, GolferWaitlistEntryRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<ICourseTimeZoneProvider, CourseTimeZoneProvider>();
builder.Services.AddScoped<ITimeProvider, Shadowbrook.Api.Infrastructure.Services.TimeZoneProvider>();
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Authentication
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection(AuthSettings.SectionName));
var authSettings = builder.Configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>()!;
var useDevAuth = authSettings.UseDevAuth;
if (useDevAuth)
{
    builder.Services.AddAuthentication("DevAuth")
        .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevAuth", null);
}
else
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

// Authorization
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAppAccess", policy =>
        policy.AddRequirements(new PermissionRequirement(Permissions.AppAccess)))
    .AddPolicy("RequireUsersManage", policy =>
        policy.AddRequirements(new PermissionRequirement(Permissions.UsersManage)));

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddWolverineHttp();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

if (!app.Environment.IsProduction() && app.Environment.EnvironmentName != "Testing")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));
    db.Database.Migrate();
}

if (!app.Environment.IsProduction() && app.Environment.EnvironmentName != "Testing")
{
    try
    {
        await E2ESeedData.EnsureAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "E2E seed data failed, continuing startup");
    }
}

app.UseDomainExceptionHandler();

var cspAuthDomain = new Uri(builder.Configuration["AzureAd:Instance"]!).Host;
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        $"connect-src 'self' https://{cspAuthDomain}; " +
        $"frame-src https://{cspAuthDomain};");
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseMiddleware<AppUserEnrichmentMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/health");

if (!app.Environment.IsProduction())
{
    app.MapDevSmsEndpoints();
}

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/debug/current-user", (ICurrentUser currentUser) => Results.Ok(new { currentUser.OrganizationId }));
}

app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();
    opts.AddMiddleware(typeof(CourseExistsMiddleware),
        chain => chain.RoutePattern?.RawText?.Contains("{courseId}") == true);
});

app.Run();
