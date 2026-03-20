using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shadowbrook.Api.Infrastructure.Data;
using Testcontainers.MsSql;
using Wolverine;

namespace Shadowbrook.Api.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly MsSqlContainer SqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static bool _containerStarted;

    private readonly string _databaseName = $"test_{Guid.NewGuid():N}"[..30];

    public async Task InitializeAsync()
    {
        await ContainerLock.WaitAsync();
        try
        {
            if (!_containerStarted)
            {
                await SqlContainer.StartAsync();
                _containerStarted = true;
            }
        }
        finally
        {
            ContainerLock.Release();
        }

        // Pre-create the database so Wolverine can connect during host startup
        await using var conn = new SqlConnection(SqlContainer.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE [{_databaseName}]";
        await cmd.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
    }

    private string GetConnectionString() =>
        SqlContainer.GetConnectionString()
            .Replace("Database=master", $"Database={_databaseName}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connectionString = GetConnectionString();

        // Override the app connection string so Wolverine's
        // UseSqlServerPersistenceAndTransport in Program.cs picks it up
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);

        builder.ConfigureServices(services =>
        {
            // Remove the app's DbContext registration
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
                options.UseSqlServer(connectionString));

            services.DisableAllExternalWolverineTransports();
            services.RunWolverineInSoloMode();

            // Use in-memory (non-durable) local queue so domain event handlers
            // are dispatched without going through the SQL outbox. With solo mode
            // the SQL outbox poller is disabled, but an in-memory queue still
            // processes messages in the same host process.
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
