using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Endpoints;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Api.Infrastructure.Repositories;
using Shadowbrook.Api.Infrastructure.Services;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate.Exceptions;
using Shadowbrook.Domain.WaitlistOfferAggregate.Exceptions;
using Shadowbrook.Domain.WalkUpWaitlistAggregate.Exceptions;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.SqlServer;

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

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);

    if (connectionString is not null)
    {
        opts.UseSqlServerPersistenceAndTransport(connectionString, "wolverine")
            .AutoProvision();
    }

    opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated;

    opts.OnException<DbUpdateConcurrencyException>()
        .RetryTimes(3);

    opts.UseEntityFrameworkCoreTransactions();
    opts.UseFluentValidation();
    opts.PublishDomainEventsFromEntityFrameworkCore<Entity, IDomainEvent>(e => e.DomainEvents);
});

builder.Services.AddSingleton<InMemoryTextMessageService>();
builder.Services.AddSingleton<ITextMessageService>(sp => sp.GetRequiredService<InMemoryTextMessageService>());
builder.Services.AddScoped<IGolferRepository, GolferRepository>();
builder.Services.AddScoped<ITeeTimeRequestRepository, TeeTimeRequestRepository>();
builder.Services.AddScoped<TeeTimeRequestService>();
builder.Services.AddScoped<IWalkUpWaitlistRepository, WalkUpWaitlistRepository>();
builder.Services.AddScoped<IWaitlistOfferRepository, WaitlistOfferRepository>();
builder.Services.AddScoped<IGolferWaitlistEntryRepository, GolferWaitlistEntryRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IShortCodeGenerator, ShortCodeGenerator>();
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
            OfferNotPendingException => StatusCodes.Status409Conflict,
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

app.MapWolverineEndpoints();

app.Run();
