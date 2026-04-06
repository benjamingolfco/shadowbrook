using Azure.Identity;
using FluentValidation;
using JasperFx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using Serilog;
using Teeforce.Api.Features.Dev;
using Teeforce.Api.Infrastructure;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Configuration;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Middleware;
using Teeforce.Api.Infrastructure.Observability;
using Teeforce.Api.Infrastructure.Repositories;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.Services;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.TenantAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;
using Teeforce.Domain.WaitlistServices;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

var isCodeGen = WolverineExtensions.IsCodeGeneration();

builder.Logging.ClearProviders();

var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

builder.Services.AddSingleton<OrganizationIdEnricher>();

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.With(services.GetRequiredService<OrganizationIdEnricher>());

    if (context.HostingEnvironment.IsDevelopment() || string.IsNullOrEmpty(appInsightsConnectionString))
    {
        configuration.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }

    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        configuration.WriteTo.ApplicationInsights(
            appInsightsConnectionString,
            TelemetryConverter.Traces);
    }
});

builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, UserContext>();

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

builder.Host.AddWolverine(builder.Environment, builder.Configuration, connectionString);

builder.Services.AddSingleton<InMemoryTextMessageService>();
builder.Services.AddScoped<DatabaseTextMessageService>();
builder.Services.AddScoped<ITextMessageService>(sp => sp.GetRequiredService<DatabaseTextMessageService>());
builder.Services.AddSingleton<IFeatureService, FeatureService>();
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IGolferRepository, GolferRepository>();
builder.Services.AddScoped<ICourseWaitlistRepository, CourseWaitlistRepository>();
builder.Services.AddScoped<ITeeTimeOpeningRepository, TeeTimeOpeningRepository>();
builder.Services.AddScoped<WaitlistMatchingService>();
builder.Services.AddScoped<WaitlistOfferClaimService>();
builder.Services.AddScoped<IWaitlistOfferRepository, WaitlistOfferRepository>();
builder.Services.AddScoped<IGolferWaitlistEntryRepository, GolferWaitlistEntryRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();

var useDevAuth = builder.Configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>()?.UseDevAuth ?? false;
if (useDevAuth)
{
    builder.Services.AddScoped<IAppUserInvitationService, NoOpAppUserInvitationService>();
    builder.Services.AddScoped<IAppUserDeletionService, NoOpAppUserDeletionService>();
    builder.Services.AddScoped<IAppUserClaimsProvider, DevAppUserClaimsProvider>();
}
else
{
    var managedIdentityClientId = builder.Configuration["AzureAd:ManagedIdentityClientId"];
    var credential = string.IsNullOrEmpty(managedIdentityClientId)
        ? new DefaultAzureCredential()
        : new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = managedIdentityClientId
        });
    builder.Services.AddSingleton(_ => new GraphServiceClient(credential));
    builder.Services.AddScoped<IAppUserInvitationService, GraphAppUserInvitationService>();
    builder.Services.AddScoped<IAppUserDeletionService, GraphAppUserDeletionService>();
    builder.Services.AddScoped<IAppUserClaimsProvider, AppUserClaimsProvider>();
}
builder.Services.AddScoped<IAppUserEmailUniquenessChecker, AppUserEmailUniquenessChecker>();
builder.Services.AddScoped<ICourseTimeZoneProvider, CourseTimeZoneProvider>();
builder.Services.AddScoped<ITimeProvider, Teeforce.Api.Infrastructure.Services.TimeZoneProvider>();
builder.Services.AddScoped<CourseContext>();
builder.Services.AddScoped<ICourseContext>(sp => sp.GetRequiredService<CourseContext>());
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddTeeforceAuth(builder.Configuration);

var authSettings = builder.Configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>()!;

builder.Services.AddWolverineHttp();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

if (!app.Environment.IsProduction() && app.Environment.EnvironmentName != "Testing" && !isCodeGen)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));
    db.Database.Migrate();

    try
    {
        await E2ESeedData.EnsureAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "E2E seed data failed, continuing startup");
    }
}

app.UseSerilogRequestLogging();
app.UseDomainExceptionHandler();

var azureAdInstance = builder.Configuration["AzureAd:Instance"];
var cspAuthDirectives = !string.IsNullOrEmpty(azureAdInstance)
    ? $"connect-src 'self' https://{new Uri(azureAdInstance).Host}; frame-src https://{new Uri(azureAdInstance).Host};"
    : "connect-src 'self';";
app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        $"default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; "
        + $"img-src 'self' data:; {cspAuthDirectives}");
    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

if (!app.Environment.IsProduction())
{
    app.MapDevSmsEndpoints();
}

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/debug/current-user", (IUserContext userContext) => Results.Ok(new { userContext.OrganizationId }));
}

app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();
    opts.AddMiddleware(typeof(CourseExistsMiddleware),
        chain => chain.RoutePattern?.RawText?.Contains("{courseId}") == true);
});

app.MapDeadLettersEndpoints()
   .RequireAuthorization();

// Seed admin accounts from configuration
var seedEmails = authSettings.GetSeedAdminEmailsList();
if (seedEmails.Length > 0 && !isCodeGen)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var emailChecker = scope.ServiceProvider.GetRequiredService<IAppUserEmailUniquenessChecker>();
    foreach (var email in seedEmails)
    {
        if (!await emailChecker.IsEmailInUse(email))
        {
            db.AppUsers.Add(await AppUser.CreateAdmin(email, emailChecker));
        }
    }

    await db.SaveChangesAsync();
}

await app.RunJasperFxCommands(args);
