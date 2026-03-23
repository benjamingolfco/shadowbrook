using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.TeeTimeRequestAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeRequestConfiguration : IEntityTypeConfiguration<TeeTimeRequest>
{
    public void Configure(EntityTypeBuilder<TeeTimeRequest> builder)
    {
        builder.ToTable("WaitlistRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasConversion<string>();
        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.CourseId, r.Date, r.TeeTime })
            .HasDatabaseName("IX_WaitlistRequests_CourseId_Date_TeeTime");
        builder.HasIndex(r => new { r.CourseId, r.Date, r.Status })
            .HasDatabaseName("IX_WaitlistRequests_CourseId_Date_Status");

        builder.HasMany(r => r.SlotFills)
            .WithOne()
            .HasForeignKey(f => f.TeeTimeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.SlotFills)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
