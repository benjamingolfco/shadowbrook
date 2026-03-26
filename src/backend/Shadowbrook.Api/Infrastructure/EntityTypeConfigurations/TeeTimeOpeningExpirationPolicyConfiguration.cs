using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.TeeTimeOpenings;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOpeningExpirationPolicyConfiguration : IEntityTypeConfiguration<TeeTimeOpeningExpirationPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeOpeningExpirationPolicy> builder)
    {
        builder.ToTable("TeeTimeOpeningExpirationPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
    }
}
