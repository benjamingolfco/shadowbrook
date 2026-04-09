using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Api.Features.TeeSheet.Policies;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeAvailabilityPolicyConfiguration : IEntityTypeConfiguration<TeeTimeAvailabilityPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeAvailabilityPolicy> builder)
    {
        builder.ToTable("TeeTimeAvailabilityPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
        builder.Property(p => p.GracePeriodExpired);
        builder.Property(p => p.SlotsRemaining);
        builder.Property(p => p.CourseId);
        builder.Property(p => p.Date);
        builder.Property(p => p.Time).HasColumnType("time");
        builder.Property(p => p.PendingOfferIds)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<HashSet<Guid>>(v, (JsonSerializerOptions?)null) ?? new HashSet<Guid>())
            .HasColumnType("nvarchar(max)");
    }
}
