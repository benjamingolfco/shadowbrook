using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeRequestConfiguration : IEntityTypeConfiguration<TeeTimeRequest>
{
    public void Configure(EntityTypeBuilder<TeeTimeRequest> builder)
    {
        builder.ToTable("WaitlistRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.WalkUpWaitlistId).HasColumnName("CourseWaitlistId");
        builder.Property(r => r.Status).HasConversion<string>();

        builder.HasIndex(r => new { r.WalkUpWaitlistId, r.TeeTime })
            .HasDatabaseName("IX_WaitlistRequests_CourseWaitlistId_TeeTime");
        builder.HasIndex(r => new { r.WalkUpWaitlistId, r.Status })
            .HasDatabaseName("IX_WaitlistRequests_CourseWaitlistId_Status");
    }
}
