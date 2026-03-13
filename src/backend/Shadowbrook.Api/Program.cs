using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Endpoints;
using Shadowbrook.Api.Endpoints.Filters;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Api.Infrastructure.Repositories;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.WalkUpWaitlist;
using Shadowbrook.Domain.WalkUpWaitlist.Exceptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
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

builder.Services.AddSingleton<InMemoryTextMessageService>();
builder.Services.AddSingleton<ITextMessageService>(sp => sp.GetRequiredService<InMemoryTextMessageService>());
builder.Services.AddScoped<IDomainEventPublisher, InProcessDomainEventPublisher>();
builder.Services.AddScoped<IWalkUpWaitlistRepository, WalkUpWaitlistRepository>();
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
builder.Services.AddScoped<IDomainEventHandler<Shadowbrook.Domain.WalkUpWaitlist.Events.GolferJoinedWaitlist>, Shadowbrook.Api.Infrastructure.Events.GolferJoinedWaitlistSmsHandler>();
builder.Services.AddScoped<IDomainEventHandler<Shadowbrook.Domain.WalkUpWaitlist.Events.TeeTimeRequestAdded>, Shadowbrook.Api.Infrastructure.Events.TeeTimeRequestAddedNotifyHandler>();
builder.Services.AddScoped<IDomainEventHandler<Shadowbrook.Domain.WalkUpWaitlist.Events.WaitlistOfferAccepted>, Shadowbrook.Api.Infrastructure.Events.WaitlistOfferAcceptedHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.EnvironmentName != "Testing")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            WaitlistAlreadyExistsException => StatusCodes.Status409Conflict,
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

app.MapWaitlistOfferEndpoints();

var api = app.MapGroup("").AddValidationFilter();
api.MapTenantEndpoints();
api.MapCourseEndpoints();
api.MapTeeSheetEndpoints();
api.MapWalkUpWaitlistEndpoints();
api.MapWalkUpJoinEndpoints();

app.Run();
