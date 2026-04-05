using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.TenantAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.OrganizationName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ContactName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ContactEmail).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ContactPhone).IsRequired().HasMaxLength(20);

        builder.HasIndex(t => t.OrganizationName).IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
