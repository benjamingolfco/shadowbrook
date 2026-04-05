using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;
using Shadowbrook.Domain.WaitlistOfferAggregate;

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

        builder.Property(o => o.NotifiedAt);
        builder.Property(o => o.IsStale).HasDefaultValue(false);
        builder.Property(o => o.RejectionReason).HasMaxLength(500);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<TeeTimeOpening>()
            .WithMany()
            .HasForeignKey(o => o.OpeningId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.Property(o => o.CourseId).IsRequired();
        builder.Property(o => o.Date).IsRequired();
        builder.Property(o => o.TeeTime).HasColumnType("time").IsRequired();

        builder.HasIndex(o => o.OpeningId);
        builder.HasIndex(o => o.CourseId);
        builder.HasIndex(o => new { o.GolferWaitlistEntryId, o.OpeningId });
    }
}
