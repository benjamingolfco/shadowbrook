using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.OrganizationAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
        builder.Property(o => o.CreatedAt);

        builder.HasIndex(o => o.Name).IsUnique();

        builder.HasShadowAuditProperties();
    }
}
