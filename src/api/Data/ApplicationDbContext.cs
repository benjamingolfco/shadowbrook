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
    public DbSet<WaitlistRequest> WaitlistRequests => Set<WaitlistRequest>();
    public DbSet<Golfer> Golfers => Set<Golfer>();
    public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();

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

        // CourseWaitlist configuration
        modelBuilder.Entity<CourseWaitlist>()
            .HasOne(w => w.Course)
            .WithMany(c => c.Waitlists)
            .HasForeignKey(w => w.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CourseWaitlist>()
            .HasIndex(w => new { w.CourseId, w.Date })
            .IsUnique();

        modelBuilder.Entity<CourseWaitlist>()
            .HasIndex(w => new { w.ShortCode, w.Date });

        modelBuilder.Entity<CourseWaitlist>()
            .Property(w => w.ShortCode)
            .HasMaxLength(4);

        modelBuilder.Entity<CourseWaitlist>()
            .Property(w => w.Status)
            .HasMaxLength(10);

        // WaitlistRequest configuration
        modelBuilder.Entity<WaitlistRequest>()
            .HasOne(wr => wr.CourseWaitlist)
            .WithMany(cw => cw.WaitlistRequests)
            .HasForeignKey(wr => wr.CourseWaitlistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WaitlistRequest>()
            .HasIndex(wr => new { wr.CourseWaitlistId, wr.TeeTime });

        modelBuilder.Entity<WaitlistRequest>()
            .HasIndex(wr => new { wr.CourseWaitlistId, wr.Status });

        // Golfer configuration
        modelBuilder.Entity<Golfer>()
            .HasIndex(g => g.Phone)
            .IsUnique();

        modelBuilder.Entity<Golfer>()
            .Property(g => g.Phone)
            .HasMaxLength(20);

        modelBuilder.Entity<Golfer>()
            .Property(g => g.FirstName)
            .HasMaxLength(100);

        modelBuilder.Entity<Golfer>()
            .Property(g => g.LastName)
            .HasMaxLength(100);

        // GolferWaitlistEntry configuration
        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasOne(e => e.CourseWaitlist)
            .WithMany(cw => cw.GolferWaitlistEntries)
            .HasForeignKey(e => e.CourseWaitlistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasOne(e => e.Golfer)
            .WithMany()
            .HasForeignKey(e => e.GolferId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasIndex(e => new { e.CourseWaitlistId, e.GolferPhone });

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasIndex(e => new { e.CourseWaitlistId, e.GolferId });

        modelBuilder.Entity<GolferWaitlistEntry>()
            .HasIndex(e => new { e.CourseWaitlistId, e.IsWalkUp, e.IsReady });

        modelBuilder.Entity<GolferWaitlistEntry>()
            .Property(e => e.GolferName)
            .HasMaxLength(200);

        modelBuilder.Entity<GolferWaitlistEntry>()
            .Property(e => e.GolferPhone)
            .HasMaxLength(20);
    }
}
