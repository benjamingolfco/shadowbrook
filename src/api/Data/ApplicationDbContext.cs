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
    public DbSet<CourseWaitlist> CourseWaitlists => Set<CourseWaitlist>();
    public DbSet<Golfer> Golfers => Set<Golfer>();
    public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();
    public DbSet<WalkUpCode> WalkUpCodes => Set<WalkUpCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.OrganizationName)
            .IsUnique();

        modelBuilder.Entity<Course>()
            .HasOne(c => c.Tenant)
            .WithMany(t => t.Courses)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add tenant scoping query filter - only filter when a tenant context exists
        modelBuilder.Entity<Course>()
            .HasQueryFilter(c => _currentUser == null || _currentUser.TenantId == null || c.TenantId == _currentUser.TenantId);

        // Add indexes for Course
        modelBuilder.Entity<Course>()
            .HasIndex(c => c.TenantId);

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

        // CourseWaitlist
        modelBuilder.Entity<CourseWaitlist>()
            .HasOne(w => w.Course)
            .WithMany()
            .HasForeignKey(w => w.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseWaitlist>()
            .HasIndex(w => new { w.CourseId, w.Date })
            .IsUnique();

        // Golfer — unique by phone (primary lookup key for find-or-create)
        modelBuilder.Entity<Golfer>()
            .HasIndex(g => g.Phone)
            .IsUnique();

        // GolferWaitlistEntry
        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasOne(e => e.CourseWaitlist)
            .WithMany()
            .HasForeignKey(e => e.CourseWaitlistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasOne(e => e.Golfer)
            .WithMany()
            .HasForeignKey(e => e.GolferId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasIndex(e => new { e.CourseWaitlistId, e.GolferId });

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasIndex(e => new { e.CourseWaitlistId, e.GolferPhone });

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasIndex(e => new { e.CourseWaitlistId, e.IsWalkUp, e.IsReady });

        // WalkUpCode — codes are globally unique per day
        modelBuilder.Entity<WalkUpCode>()
            .HasOne(w => w.Course)
            .WithMany()
            .HasForeignKey(w => w.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WalkUpCode>()
            .HasIndex(w => new { w.Code, w.Date })
            .IsUnique();

        modelBuilder.Entity<WalkUpCode>()
            .HasIndex(w => new { w.CourseId, w.Date });
    }
}
