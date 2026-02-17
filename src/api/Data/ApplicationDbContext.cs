using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>()
            .Property(t => t.OrganizationName)
            .UseCollation("NOCASE");

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.OrganizationName)
            .IsUnique();

        modelBuilder.Entity<Course>()
            .HasOne(c => c.Tenant)
            .WithMany(t => t.Courses)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Course)
            .WithMany()
            .HasForeignKey(b => b.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => new { b.CourseId, b.Date, b.Time });
    }
}
