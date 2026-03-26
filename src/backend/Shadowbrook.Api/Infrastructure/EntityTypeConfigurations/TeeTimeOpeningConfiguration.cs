using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.TeeTimeOpeningAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOpeningConfiguration : IEntityTypeConfiguration<TeeTimeOpening>
{
    public void Configure(EntityTypeBuilder<TeeTimeOpening> builder)
    {
        builder.ToTable("TeeTimeOpenings");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.TeeTime).HasColumnType("time");
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(10);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(o => o.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(o => new { o.CourseId, o.Date, o.TeeTime });
        builder.HasIndex(o => new { o.CourseId, o.Date, o.Status });
    }
}
