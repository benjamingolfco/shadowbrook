using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Features.WaitlistOffers;
using Shadowbrook.Api.Features.WalkUpWaitlist;
using Shadowbrook.Api.Infrastructure.Dev;
using Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.TenantAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null) : DbContext(options)
{
    // Snapshot TenantId at DbContext construction time so the query filter sees the correct
    // tenant for this request. EF Core evaluates query filters referencing 'this' against the
    // specific DbContext instance executing the query, so a new instance per request is required.
    public Guid? CurrentTenantId { get; } = currentUser?.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WalkUpWaitlist> WalkUpWaitlists => Set<WalkUpWaitlist>();
    public DbSet<TeeTimeRequest> TeeTimeRequests => Set<TeeTimeRequest>();
    public DbSet<TeeTimeSlotFill> TeeTimeSlotFills => Set<TeeTimeSlotFill>();
    public DbSet<Golfer> Golfers => Set<Golfer>();
    public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();
    public DbSet<WaitlistOffer> WaitlistOffers => Set<WaitlistOffer>();
    public DbSet<TeeTimeOfferPolicy> TeeTimeOfferPolicies => Set<TeeTimeOfferPolicy>();
    public DbSet<TeeTimeRequestExpirationPolicy> TeeTimeRequestExpirationPolicies => Set<TeeTimeRequestExpirationPolicy>();
    public DbSet<DevSmsMessage> DevSmsMessages => Set<DevSmsMessage>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entry.Property("UpdatedAt").CurrentValue = now;
            entry.Property("UpdatedBy").CurrentValue = userId;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Dev tooling — no tenant filter, no audit properties
        modelBuilder.Entity<DevSmsMessage>(b =>
        {
            b.ToTable("DevSmsMessages");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).ValueGeneratedNever();
            b.Property(m => m.From).IsRequired().HasMaxLength(20);
            b.Property(m => m.To).IsRequired().HasMaxLength(20);
            b.Property(m => m.Body).IsRequired();
            b.HasIndex(m => m.Timestamp);
        });

        // Apply tenant query filter — reference 'this' so EF Core substitutes the actual
        // DbContext instance at query time, ensuring CurrentTenantId is re-read per request.
        modelBuilder.Entity<Course>()
            .HasQueryFilter(c => CurrentTenantId == null || c.TenantId == CurrentTenantId);

        // Apply domain entity configurations
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new WalkUpWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeRequestConfiguration());
        modelBuilder.ApplyConfiguration(new GolferConfiguration());
        modelBuilder.ApplyConfiguration(new GolferWaitlistEntryConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistOfferConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeSlotFillConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOfferPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeRequestExpirationPolicyConfiguration());
    }
}
