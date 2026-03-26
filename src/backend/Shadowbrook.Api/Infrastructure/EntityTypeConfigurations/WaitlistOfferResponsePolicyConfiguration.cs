using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.TeeTimeOpenings;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WaitlistOfferResponsePolicyConfiguration : IEntityTypeConfiguration<WaitlistOfferResponsePolicy>
{
    public void Configure(EntityTypeBuilder<WaitlistOfferResponsePolicy> builder)
    {
        builder.ToTable("WaitlistOfferResponsePolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
    }
}
