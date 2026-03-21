using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.WaitlistOffers;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeOfferPolicyConfiguration : IEntityTypeConfiguration<TeeTimeOfferPolicy>
{
    public void Configure(EntityTypeBuilder<TeeTimeOfferPolicy> builder)
    {
        builder.ToTable("TeeTimeOfferPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
    }
}
