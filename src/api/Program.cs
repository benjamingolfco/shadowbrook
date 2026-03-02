using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Endpoints;
using Shadowbrook.Api.Events;
using Shadowbrook.Api.Events.Handlers;
using Shadowbrook.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Rate limiting — per-IP limits to protect public unauthenticated endpoints.
// PermitLimit is read from configuration so tests can override it via IConfiguration.
var verifyPermitLimit = builder.Configuration.GetValue("RateLimiting:WalkUpVerify:PermitLimit", defaultValue: 10);
var verifyWindowSeconds = builder.Configuration.GetValue("RateLimiting:WalkUpVerify:WindowSeconds", defaultValue: 300);

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter =
            context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? ((int)retryAfter.TotalSeconds).ToString()
                : verifyWindowSeconds.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many verification attempts. Please wait a few minutes before trying again." },
            cancellationToken);
    };

    // Per-IP fixed window — protects the 4-digit code (10,000 combination) enumeration surface.
    // X-Forwarded-For is preferred so the real client IP is used when behind a load balancer.
    options.AddPolicy(WalkUpEndpoints.RateLimitPolicyName, httpContext =>
    {
        var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = verifyPermitLimit,
                Window = TimeSpan.FromSeconds(verifyWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
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
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<ITextMessageService, ConsoleTextMessageService>();
builder.Services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();
builder.Services.AddScoped<IDomainEventHandler<GolferJoinedWaitlist>, SendWaitlistConfirmationSmsHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (app.Environment.EnvironmentName != "Testing")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<TenantClaimMiddleware>();

app.MapHealthChecks("/health");

if (app.Environment.EnvironmentName == "Testing")
{
    app.MapGet("/debug/current-user", (ICurrentUser currentUser) =>
    {
        return Results.Ok(new { TenantId = currentUser.TenantId });
    });
}

app.MapTenantEndpoints();
app.MapCourseEndpoints();
app.MapTeeSheetEndpoints();
app.MapWalkUpEndpoints();

app.Run();
