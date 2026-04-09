using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.TeeTimeAggregate;
using Teeforce.Domain.TeeTimeOfferAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOfferConfiguration : IEntityTypeConfiguration<TeeTimeOffer>
{
    public void Configure(EntityTypeBuilder<TeeTimeOffer> builder)
    {
        builder.ToTable("TeeTimeOffers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.TeeTimeId).IsRequired();
        builder.Property(o => o.GolferWaitlistEntryId).IsRequired();
        builder.Property(o => o.GolferId).IsRequired();
        builder.Property(o => o.GroupSize);
        builder.Property(o => o.Token).IsRequired();
        builder.HasIndex(o => o.Token).IsUnique();

        builder.Property(o => o.CourseId).IsRequired();
        builder.Property(o => o.Date).IsRequired();
        builder.Property(o => o.Time).HasColumnType("time").IsRequired();

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.RejectionReason).HasMaxLength(500);
        builder.Property(o => o.IsStale).HasDefaultValue(false);
        builder.Property(o => o.NotifiedAt);

        builder.HasOne<TeeTime>()
            .WithMany()
            .HasForeignKey(o => o.TeeTimeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.TeeTimeId);
        builder.HasIndex(o => o.CourseId);
        builder.HasIndex(o => new { o.TeeTimeId, o.GolferId, o.Status });

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
