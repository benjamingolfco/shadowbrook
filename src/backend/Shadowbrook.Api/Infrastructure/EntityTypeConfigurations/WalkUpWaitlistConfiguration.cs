using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;
using Shadowbrook.Domain.WalkUpWaitlist;
using WalkUpWaitlistEntity = Shadowbrook.Domain.WalkUpWaitlist.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WalkUpWaitlistConfiguration : IEntityTypeConfiguration<WalkUpWaitlistEntity>
{
    public void Configure(EntityTypeBuilder<WalkUpWaitlistEntity> builder)
    {
        builder.ToTable("CourseWaitlists");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(w => w.ShortCode).IsRequired().HasMaxLength(4);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(w => w.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.CourseId, w.Date }).IsUnique();
        builder.HasIndex(w => new { w.ShortCode, w.Date });

        builder.HasMany(w => w.TeeTimeRequests)
            .WithOne()
            .HasForeignKey(r => r.WalkUpWaitlistId)
            .HasConstraintName("FK_WaitlistRequests_CourseWaitlists_CourseWaitlistId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.TeeTimeRequests)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
