using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WaitlistRequestAcceptanceConfiguration : IEntityTypeConfiguration<WaitlistRequestAcceptance>
{
    public void Configure(EntityTypeBuilder<WaitlistRequestAcceptance> builder)
    {
        builder.ToTable("WaitlistRequestAcceptances");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<TeeTimeRequest>()
            .WithMany()
            .HasForeignKey(a => a.WaitlistRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<GolferWaitlistEntry>()
            .WithMany()
            .HasForeignKey(a => a.GolferWaitlistEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // One acceptance per request — prevents double-booking
        builder.HasIndex(a => a.WaitlistRequestId)
            .IsUnique()
            .HasDatabaseName("IX_WaitlistRequestAcceptances_WaitlistRequestId");
    }
}
