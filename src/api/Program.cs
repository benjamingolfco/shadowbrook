using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Data;
using Shadowbrook.Api.Endpoints;
using Shadowbrook.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins is { Length: > 0 })
            policy.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader();
    });
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=shadowbrook.db"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddScoped<ITextMessageService, ConsoleTextMessageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}
else if (app.Environment.EnvironmentName != "Testing")
{
    // Apply migrations in non-Development/non-Testing environments (Azure)
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.UseCors();

app.MapHealthChecks("/health");
app.MapCourseEndpoints();
app.MapTeeSheetEndpoints();

app.Run();
