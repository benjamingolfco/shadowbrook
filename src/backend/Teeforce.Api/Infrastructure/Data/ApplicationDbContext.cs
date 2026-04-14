using Microsoft.EntityFrameworkCore;
using Teeforce.Api.Features.Bookings;
using Teeforce.Api.Features.TeeSheet.Policies;
using Teeforce.Api.Features.Waitlist;
using Teeforce.Api.Features.Waitlist.Policies;
using Teeforce.Api.Infrastructure.Auth;
using Teeforce.Api.Infrastructure.Dev;
using Teeforce.Api.Infrastructure.EntityTypeConfigurations;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.BookingAggregate;
using Teeforce.Domain.Common;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;
using Teeforce.Domain.OrganizationAggregate;
using Teeforce.Domain.TeeSheetAggregate;
using Teeforce.Domain.TeeTimeAggregate;
using Teeforce.Domain.TeeTimeOfferAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;
using Teeforce.Domain.TenantAggregate;
using Teeforce.Domain.CoursePricingAggregate;
using Teeforce.Domain.WaitlistOfferAggregate;

namespace Teeforce.Api.Infrastructure.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IUserContext? userContext = null) : DbContext(options)
{
    // Snapshot OrganizationId at DbContext construction time so the query filter sees the correct
    // organization for this request. EF Core evaluates query filters referencing 'this' against the
    // specific DbContext instance executing the query, so a new instance per request is required.
    public Guid? CurrentOrganizationId { get; } = userContext?.OrganizationId;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<CourseWaitlist> CourseWaitlists => Set<CourseWaitlist>();
    public DbSet<TeeSheet> TeeSheets => Set<TeeSheet>();
    public DbSet<TeeTime> TeeTimes => Set<TeeTime>();
    public DbSet<TeeTimeOpening> TeeTimeOpenings => Set<TeeTimeOpening>();
    public DbSet<Golfer> Golfers => Set<Golfer>();
    public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();
    public DbSet<WaitlistOffer> WaitlistOffers => Set<WaitlistOffer>();
    public DbSet<TeeTimeOpeningExpirationPolicy> TeeTimeOpeningExpirationPolicies => Set<TeeTimeOpeningExpirationPolicy>();
    public DbSet<TeeTimeOpeningOfferPolicy> TeeTimeOpeningOfferPolicies => Set<TeeTimeOpeningOfferPolicy>();
    public DbSet<WaitlistOfferResponsePolicy> WaitlistOfferResponsePolicies => Set<WaitlistOfferResponsePolicy>();
    public DbSet<TeeTimeOffer> TeeTimeOffers => Set<TeeTimeOffer>();
    public DbSet<TeeTimeAvailabilityPolicy> TeeTimeAvailabilityPolicies => Set<TeeTimeAvailabilityPolicy>();
    public DbSet<CoursePricingSettings> CoursePricingSettings => Set<CoursePricingSettings>();
    public DbSet<DevSmsMessage> DevSmsMessages => Set<DevSmsMessage>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = userContext?.AppUserId?.ToString();

        foreach (var entry in ChangeTracker.Entries<Entity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            // Only aggregate roots carry shadow audit properties (configured via
            // HasShadowAuditProperties). Owned/child entities (e.g. TeeSheetInterval,
            // TeeTimeClaim) inherit from Entity but have no audit columns — skip them.
            if (entry.Metadata.FindProperty("UpdatedAt") is null)
            {
                continue;
            }
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

        modelBuilder.Entity<AppUser>()
            .HasQueryFilter(u => !u.IsDeleted);

        // Apply domain entity configurations
        modelBuilder.ApplyConfiguration(new OrganizationConfiguration());
        modelBuilder.ApplyConfiguration(new AppUserConfiguration());
        modelBuilder.ApplyConfiguration(new CourseConfiguration());
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new CourseWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new WalkUpWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new OnlineWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new TeeSheetConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOpeningConfiguration());
        modelBuilder.ApplyConfiguration(new GolferConfiguration());
        modelBuilder.ApplyConfiguration(new GolferWaitlistEntryConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistOfferConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOpeningExpirationPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOpeningOfferPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistOfferResponsePolicyConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeOfferConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeAvailabilityPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new CoursePricingSettingsConfiguration());
    }
}
