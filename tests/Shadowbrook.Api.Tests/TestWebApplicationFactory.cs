using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Respawn;
using Shadowbrook.Api.Infrastructure.Data;
using Shadowbrook.Domain.AppUserAggregate;
using Testcontainers.MsSql;
using Wolverine;

namespace Shadowbrook.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner? respawner;
    private string connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await this.sqlContainer.StartAsync();
        this.connectionString = this.sqlContainer.GetConnectionString();
    }

    public HttpClient CreateAuthenticatedClient(string identityId = "test-admin-oid")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", identityId);
        return client;
    }

    public async Task SeedTestAdminAsync(string identityId = "test-admin-oid")
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!await db.AppUsers.AnyAsync(u => u.IdentityId == identityId))
        {
            var admin = AppUser.Create(identityId, "admin@test.com", "Test Admin", AppUserRole.Admin, null);
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

        builder.ConfigureServices(services =>
        {
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true)
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(this.connectionString));

            services.DisableAllExternalWolverineTransports();
            services.RunWolverineInSoloMode();

            services.ConfigureWolverine(opts =>
                opts.DefaultLocalQueue.BufferedInMemory());
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
