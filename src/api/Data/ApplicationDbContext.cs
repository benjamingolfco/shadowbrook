using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isSqlite = Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        modelBuilder.Entity<Tenant>()
            .Property(t => t.OrganizationName)
            .UseCollation(isSqlite ? "NOCASE" : "Latin1_General_CI_AS");

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.OrganizationName)
            .IsUnique();

        modelBuilder.Entity<Course>()
            .HasOne(c => c.Tenant)
            .WithMany(t => t.Courses)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add tenant scoping query filter
        modelBuilder.Entity<Course>()
            .HasQueryFilter(c => _currentUser == null || _currentUser.TenantId == null || c.TenantId == _currentUser.TenantId);

        // Add indexes for Course
        modelBuilder.Entity<Course>()
            .HasIndex(c => c.TenantId);

        modelBuilder.Entity<Course>()
            .Property(c => c.Name)
            .UseCollation(isSqlite ? "NOCASE" : "Latin1_General_CI_AS");

        modelBuilder.Entity<Course>()
            .HasIndex(c => new { c.TenantId, c.Name })
            .IsUnique();

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Course)
            .WithMany()
            .HasForeignKey(b => b.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.CourseId, b.Date, b.Time });
    }
}
