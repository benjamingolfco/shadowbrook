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
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=shadowbrook.db"));
}

builder.Services.AddScoped<ITextMessageService, ConsoleTextMessageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

if (app.Environment.EnvironmentName != "Testing")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DefaultConnection")))
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

app.UseCors();

app.MapHealthChecks("/health");
app.MapCourseEndpoints();
app.MapTeeSheetEndpoints();

app.Run();
