using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Features.Bookings;
using Shadowbrook.Api.Features.Waitlist;
using Shadowbrook.Api.Features.Waitlist.Policies;
using Shadowbrook.Api.Infrastructure.Dev;
using Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.OrganizationAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.TenantAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

namespace Shadowbrook.Api.Infrastructure.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null) : DbContext(options)
{
    // Snapshot OrganizationId at DbContext construction time so the query filter sees the correct
    // organization for this request. EF Core evaluates query filters referencing 'this' against the
    // specific DbContext instance executing the query, so a new instance per request is required.
    public Guid? CurrentOrganizationId { get; } = currentUser?.OrganizationId;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<CourseWaitlist> CourseWaitlists => Set<CourseWaitlist>();
    public DbSet<TeeTimeOpening> TeeTimeOpenings => Set<TeeTimeOpening>();
    public DbSet<Golfer> Golfers => Set<Golfer>();
    public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();
    public DbSet<WaitlistOffer> WaitlistOffers => Set<WaitlistOffer>();
    public DbSet<TeeTimeOpeningExpirationPolicy> TeeTimeOpeningExpirationPolicies => Set<TeeTimeOpeningExpirationPolicy>();
    public DbSet<TeeTimeOpeningOfferPolicy> TeeTimeOpeningOfferPolicies => Set<TeeTimeOpeningOfferPolicy>();
    public DbSet<WaitlistOfferResponsePolicy> WaitlistOfferResponsePolicies => Set<WaitlistOfferResponsePolicy>();
    public DbSet<DevSmsMessage> DevSmsMessages => Set<DevSmsMessage>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = currentUser?.AppUserId?.ToString();

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

        // Apply organization query filter — reference 'this' so EF Core substitutes the actual
        // DbContext instance at query time, ensuring CurrentOrganizationId is re-read per request.
        modelBuilder.Entity<Course>()
            .HasQueryFilter(c => CurrentOrganizationId == null || c.OrganizationId == CurrentOrganizationId);

        // Apply domain entity configurations
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new CourseAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new CourseWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new WalkUpWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new OnlineWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOpeningConfiguration());
        modelBuilder.ApplyConfiguration(new GolferConfiguration());
        modelBuilder.ApplyConfiguration(new GolferWaitlistEntryConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistOfferConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOpeningExpirationPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOpeningOfferPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistOfferResponsePolicyConfiguration());
    }
}
