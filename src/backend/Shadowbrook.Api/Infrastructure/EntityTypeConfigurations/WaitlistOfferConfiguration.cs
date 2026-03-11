using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WaitlistOfferConfiguration : IEntityTypeConfiguration<WaitlistOffer>
{
    public void Configure(EntityTypeBuilder<WaitlistOffer> builder)
    {
        builder.ToTable("WaitlistOffers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.GolferPhone).IsRequired().HasMaxLength(20);
        builder.Property(o => o.CourseName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Status).HasConversion<string>().IsRequired();

        builder.HasOne<GolferWaitlistEntry>()
            .WithMany()
            .HasForeignKey(o => o.GolferWaitlistEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index for inbound SMS lookup: find pending offers by phone
        builder.HasIndex(o => new { o.GolferPhone, o.Status })
            .HasDatabaseName("IX_WaitlistOffers_GolferPhone_Status")
            .HasFilter("[Status] = 'Pending'");

        // Index for checking existing offers per request
        builder.HasIndex(o => o.TeeTimeRequestId)
            .HasDatabaseName("IX_WaitlistOffers_TeeTimeRequestId");
    }
}
