using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Endpoints;
using Shadowbrook.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

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

app.Run();
