using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.CourseWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class CourseWaitlistConfiguration : IEntityTypeConfiguration<CourseWaitlist>
{
    public void Configure(EntityTypeBuilder<CourseWaitlist> builder)
    {
        builder.ToTable("CourseWaitlists");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.HasDiscriminator<string>("WaitlistType")
            .HasValue<WalkUpWaitlist>("WalkUp")
            .HasValue<OnlineWaitlist>("Online");

        builder.Property<string>("WaitlistType").HasMaxLength(10);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(w => w.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(w => new { w.CourseId, w.Date }).IsUnique();
    }
}

public class WalkUpWaitlistConfiguration : IEntityTypeConfiguration<WalkUpWaitlist>
{
    public void Configure(EntityTypeBuilder<WalkUpWaitlist> builder)
    {
        builder.Property(w => w.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(w => w.ShortCode).IsRequired().HasMaxLength(4);

        builder.HasIndex(w => new { w.ShortCode, w.Date });
    }
}

public class OnlineWaitlistConfiguration : IEntityTypeConfiguration<OnlineWaitlist>
{
    public void Configure(EntityTypeBuilder<OnlineWaitlist> builder)
    {
        // No additional properties yet
    }
}
