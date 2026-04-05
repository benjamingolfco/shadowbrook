using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseWaitlistAggregate;
using Teeforce.Domain.GolferAggregate;
using Teeforce.Domain.GolferWaitlistEntryAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class GolferWaitlistEntryConfiguration : IEntityTypeConfiguration<GolferWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<GolferWaitlistEntry> builder)
    {
        builder.ToTable("GolferWaitlistEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.GroupSize).HasDefaultValue(1);

        builder.HasDiscriminator(e => e.IsWalkUp)
            .HasValue<WalkUpGolferWaitlistEntry>(true)
            .HasValue<OnlineGolferWaitlistEntry>(false);

        builder.Property(e => e.WindowStart).HasColumnType("datetime2");
        builder.Property(e => e.WindowEnd).HasColumnType("datetime2");

        builder.HasOne<Golfer>()
            .WithMany()
            .HasForeignKey(e => e.GolferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CourseWaitlist>()
            .WithMany()
            .HasForeignKey(e => e.CourseWaitlistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CourseWaitlistId, e.GolferId })
            .HasFilter("[RemovedAt] IS NULL")
            .IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(e => e.GolferId);
    }
}
