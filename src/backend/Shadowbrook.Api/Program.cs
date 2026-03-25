using Azure.Monitor.OpenTelemetry.AspNetCore;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Features.Dev;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Middleware;
using Shadowbrook.Api.Infrastructure.Observability;
using Shadowbrook.Api.Infrastructure.Repositories;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.TenantAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;
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
    .Enrich.With(services.GetRequiredService<TenantIdEnricher>())
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

builder.Services.AddSingleton<TenantIdEnricher>();

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

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        var originPattern = builder.Configuration["Cors:AllowedOriginPattern"];

        if (!string.IsNullOrEmpty(originPattern))
        {
            var regex = new System.Text.RegularExpressions.Regex(originPattern);
            policy.SetIsOriginAllowed(origin => regex.IsMatch(origin))
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else if (origins is { Length: > 0 })
        {
            policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
        }
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
builder.Services.AddScoped<ITeeTimeRequestRepository, TeeTimeRequestRepository>();
builder.Services.AddScoped<TeeTimeRequestService>();
builder.Services.AddScoped<IWalkUpWaitlistRepository, WalkUpWaitlistRepository>();
builder.Services.AddScoped<IWaitlistOfferRepository, WaitlistOfferRepository>();
builder.Services.AddScoped<IGolferWaitlistEntryRepository, GolferWaitlistEntryRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<ICourseTimeZoneProvider, CourseTimeZoneProvider>();
builder.Services.AddScoped<ITimeProvider, Shadowbrook.Api.Infrastructure.Services.TimeZoneProvider>();
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddWolverineHttp();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.EnvironmentName != "Testing")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.SetCommandTimeout(TimeSpan.FromSeconds(120));
    db.Database.Migrate();
}

app.UseExceptionHandler(error => error.Run(async context =>
{
    var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    if (ex is DomainException domainEx)
    {
        context.Response.StatusCode = domainEx switch
        {
            DuplicateTeeTimeRequestException => StatusCodes.Status409Conflict,
            GolferAlreadyOnWaitlistException => StatusCodes.Status409Conflict,
            WaitlistAlreadyExistsException => StatusCodes.Status409Conflict,
            WaitlistNotClosedException => StatusCodes.Status409Conflict,
            OfferNotPendingException => StatusCodes.Status409Conflict,
            TeeTimePastException => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest
        };
        await context.Response.WriteAsJsonAsync(new { error = domainEx.Message });
    }

}));

app.UseCors();
app.UseMiddleware<TenantClaimMiddleware>();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapDevSmsEndpoints();
}

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/debug/current-user", (ICurrentUser currentUser) => Results.Ok(new { TenantId = currentUser.TenantId }));
}

app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();
    opts.AddMiddleware(typeof(CourseExistsMiddleware),
        chain => chain.RoutePattern?.RawText?.Contains("{courseId}") == true);
});

app.Run();
