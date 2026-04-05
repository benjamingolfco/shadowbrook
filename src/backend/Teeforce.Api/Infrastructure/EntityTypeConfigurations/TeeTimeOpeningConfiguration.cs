using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeOpeningAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOpeningConfiguration : IEntityTypeConfiguration<TeeTimeOpening>
{
    public void Configure(EntityTypeBuilder<TeeTimeOpening> builder)
    {
        builder.ToTable("TeeTimeOpenings");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.ComplexProperty(o => o.TeeTime, t =>
            t.Property(x => x.Value).HasColumnName("TeeTime").HasColumnType("datetime2"));

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(10);

        builder.OwnsMany(o => o.ClaimedSlots, cs =>
        {
            cs.ToTable("TeeTimeOpeningClaimedSlots");
            cs.WithOwner().HasForeignKey("TeeTimeOpeningId");
            cs.HasKey("TeeTimeOpeningId", nameof(ClaimedSlot.BookingId));
            cs.Property(x => x.BookingId).ValueGeneratedNever();
            cs.Property(x => x.GolferId);
            cs.Property(x => x.GroupSize);
            cs.Property(x => x.ClaimedAt);
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(o => o.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(o => new { o.CourseId, o.Status });
    }
}
