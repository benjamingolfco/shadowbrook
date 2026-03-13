using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WaitlistOfferConfiguration : IEntityTypeConfiguration<WaitlistOffer>
{
    public void Configure(EntityTypeBuilder<WaitlistOffer> builder)
    {
        builder.ToTable("WaitlistOffers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Token).IsRequired();
        builder.HasIndex(o => o.Token).IsUnique();

        builder.Property(o => o.CourseName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.GolferName).IsRequired().HasMaxLength(200);
        builder.Property(o => o.GolferPhone).IsRequired().HasMaxLength(20);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(o => o.TeeTimeRequestId);
        builder.HasIndex(o => new { o.GolferWaitlistEntryId, o.TeeTimeRequestId });
    }
}
