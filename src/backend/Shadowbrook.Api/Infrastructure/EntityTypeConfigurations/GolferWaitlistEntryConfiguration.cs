using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class GolferWaitlistEntryConfiguration : IEntityTypeConfiguration<GolferWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<GolferWaitlistEntry> builder)
    {
        builder.ToTable("GolferWaitlistEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.GolferName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.GolferPhone).IsRequired().HasMaxLength(20);
        builder.Property(e => e.GroupSize).HasDefaultValue(1);

        builder.HasOne<WalkUpWaitlistEntity>()
            .WithMany()
            .HasForeignKey(e => e.CourseWaitlistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Golfer)
            .WithMany()
            .HasForeignKey(e => e.GolferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.CourseWaitlistId, e.GolferPhone })
            .HasFilter("[RemovedAt] IS NULL");

        builder.HasIndex(e => e.GolferId);
    }
}
