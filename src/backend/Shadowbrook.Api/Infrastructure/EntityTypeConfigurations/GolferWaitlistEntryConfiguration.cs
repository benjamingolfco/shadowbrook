using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.GolferAggregate;
using Shadowbrook.Domain.GolferWaitlistEntryAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class GolferWaitlistEntryConfiguration : IEntityTypeConfiguration<GolferWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<GolferWaitlistEntry> builder)
    {
        builder.ToTable("GolferWaitlistEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.GroupSize).HasDefaultValue(1);

        // TPH discriminator — EF Core uses IsWalkUp as the discriminator column.
        // WalkUpGolferWaitlistEntry maps to IsWalkUp = true, OnlineGolferWaitlistEntry to false.
        builder.HasDiscriminator(e => e.IsWalkUp)
            .HasValue<WalkUpGolferWaitlistEntry>(true)
            .HasValue<OnlineGolferWaitlistEntry>(false);

        builder.Property(e => e.WindowStart)
            .HasColumnType("time");

        builder.Property(e => e.WindowEnd)
            .HasColumnType("time");

        builder.HasOne<Golfer>()
            .WithMany()
            .HasForeignKey(e => e.GolferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<WalkUpWaitlist>()
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
