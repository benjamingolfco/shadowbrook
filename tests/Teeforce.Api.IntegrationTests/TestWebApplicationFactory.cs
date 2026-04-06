using JasperFx.CommandLine;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Respawn;
using Teeforce.Api.Infrastructure.Data;
using Teeforce.Api.Infrastructure.Services;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.Services;
using Testcontainers.MsSql;
using Wolverine;

namespace Teeforce.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner? respawner;
    private string connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        // Required so that RunJasperFxCommands in Program.cs starts the host when
        // running under WebApplicationFactory (instead of blocking on app.Run()).
        JasperFxEnvironment.AutoStartHost = true;

        await this.sqlContainer.StartAsync();
        this.connectionString = this.sqlContainer.GetConnectionString();
    }

    public HttpClient CreateAuthenticatedClient(string email = "test-admin@test.com")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", email);
        return client;
    }

    public async Task SeedTestAdminAsync(string email = "test-admin@test.com")
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailChecker = scope.ServiceProvider.GetRequiredService<IAppUserEmailUniquenessChecker>();

        if (!await db.AppUsers.AnyAsync(u => u.Email == email))
        {
            var admin = await AppUser.CreateAdmin(email, emailChecker);
            admin.Activate();
            db.AppUsers.Add(admin);
            await db.SaveChangesAsync();
        }
    }

    public async Task ResetDatabaseAsync()
    {
        if (this.respawner is null)
        {
            await using var conn = new SqlConnection(this.connectionString);
            await conn.OpenAsync();
            // Wolverine tables live in the "wolverine" schema (configured in Program.cs),
            // so SchemasToInclude = ["dbo"] already excludes them. Only EF migration
            // history needs explicit protection.
            this.respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = ["dbo"],
                TablesToIgnore = [
                    new Respawn.Graph.Table("__EFMigrationsHistory"),
                ]
            });
        }

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync();
        await this.respawner.ResetAsync(connection);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            // Wolverine background services throw TaskCanceledException on host shutdown — expected
        }

        await this.sqlContainer.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            // Wolverine background services throw TaskCanceledException on host shutdown — expected
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", this.connectionString);
        builder.UseSetting("Auth:UseDevAuth", "true");
        builder.UseSetting("Auth:SeedAdminEmails", "");
        builder.UseSetting("App:FrontendUrl", "http://localhost:3000");

        builder.ConfigureServices(services =>
        {
            services.DisableAllExternalWolverineTransports();
            services.RunWolverineInSoloMode();

            // Override ISmsSender — "Testing" environment falls through to TelnyxSmsSender in
            // Program.cs, but integration tests need DatabaseSmsSender (no real credentials).
            services.AddScoped<DatabaseSmsSender>();
            services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<DatabaseSmsSender>());
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        return host;
    }
}
