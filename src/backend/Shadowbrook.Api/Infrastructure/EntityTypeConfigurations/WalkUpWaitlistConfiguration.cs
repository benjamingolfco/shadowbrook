using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.WalkUpWaitlistAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WalkUpWaitlistConfiguration : IEntityTypeConfiguration<WalkUpWaitlist>
{
    public void Configure(EntityTypeBuilder<WalkUpWaitlist> builder)
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

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(w => new { w.CourseId, w.Date }).IsUnique();
        builder.HasIndex(w => new { w.ShortCode, w.Date });
    }
}
