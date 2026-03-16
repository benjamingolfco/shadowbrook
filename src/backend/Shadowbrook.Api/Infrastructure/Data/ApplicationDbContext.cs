using Microsoft.EntityFrameworkCore;
using Shadowbrook.Api.Auth;
using Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;
using Shadowbrook.Api.Infrastructure.Events;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.BookingAggregate;
using Shadowbrook.Domain.Common;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    ICurrentUser? currentUser = null,
    IDomainEventPublisher? eventPublisher = null) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WalkUpWaitlist> WalkUpWaitlists => Set<WalkUpWaitlist>();
    public DbSet<TeeTimeRequest> TeeTimeRequests => Set<TeeTimeRequest>();
    public DbSet<Golfer> Golfers => Set<Golfer>();
    public DbSet<GolferWaitlistEntry> GolferWaitlistEntries => Set<GolferWaitlistEntry>();
    public DbSet<WaitlistOffer> WaitlistOffers => Set<WaitlistOffer>();
    public DbSet<WaitlistRequestAcceptance> WaitlistRequestAcceptances => Set<WaitlistRequestAcceptance>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);

        var domainEvents = ChangeTracker.Entries<Entity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var entity in ChangeTracker.Entries<Entity>())
        {
            entity.Entity.ClearDomainEvents();
        }

        if (eventPublisher is not null)
        {
            foreach (var domainEvent in domainEvents)
            {
                await eventPublisher.PublishAsync(domainEvent, cancellationToken);
            }
        }

        return result;
    }

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

        modelBuilder.Entity<Course>()
            .HasQueryFilter(c => currentUser == null || currentUser.TenantId == null || c.TenantId == currentUser.TenantId);

        modelBuilder.Entity<Course>()
            .HasIndex(c => c.TenantId);

        modelBuilder.Entity<Course>()
            .HasIndex(c => new { c.TenantId, c.Name })
            .IsUnique();

        // Apply domain entity configurations
        modelBuilder.ApplyConfiguration(new BookingConfiguration());
        modelBuilder.ApplyConfiguration(new WalkUpWaitlistConfiguration());
        modelBuilder.ApplyConfiguration(new TeeTimeRequestConfiguration());
        modelBuilder.ApplyConfiguration(new GolferConfiguration());
        modelBuilder.ApplyConfiguration(new GolferWaitlistEntryConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistOfferConfiguration());
        modelBuilder.ApplyConfiguration(new WaitlistRequestAcceptanceConfiguration());
    }
}
