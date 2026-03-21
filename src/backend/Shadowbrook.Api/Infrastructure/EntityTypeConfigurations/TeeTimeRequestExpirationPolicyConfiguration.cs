using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.WalkUpWaitlist;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeRequestExpirationPolicyConfiguration : IEntityTypeConfiguration<TeeTimeRequestExpirationPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeRequestExpirationPolicy> builder)
    {
        builder.ToTable("TeeTimeRequestExpirationPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
    }
}
