using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;
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

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(r => r.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.CourseId, r.Date, r.TeeTime })
            .HasDatabaseName("IX_WaitlistRequests_CourseId_Date_TeeTime");
        builder.HasIndex(r => new { r.CourseId, r.Date, r.Status })
            .HasDatabaseName("IX_WaitlistRequests_CourseId_Date_Status");
    }
}
