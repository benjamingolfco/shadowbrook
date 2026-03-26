using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.TeeTimeOpenings;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOpeningOfferPolicyConfiguration : IEntityTypeConfiguration<TeeTimeOpeningOfferPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeOpeningOfferPolicy> builder)
    {
        builder.ToTable("TeeTimeOpeningOfferPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
    }
}
