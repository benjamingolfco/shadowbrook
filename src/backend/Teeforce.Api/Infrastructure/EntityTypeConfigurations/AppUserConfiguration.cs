using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.AppUserAggregate;
using Teeforce.Domain.OrganizationAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.HasIndex(u => u.IdentityId).IsUnique().HasFilter("[IdentityId] IS NOT NULL");
        builder.Property(u => u.IdentityId).IsRequired(false).HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.FirstName).IsRequired(false).HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired(false).HasMaxLength(100);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.IsActive);
        builder.Property(u => u.CreatedAt);
        builder.Property(u => u.InviteSentAt).IsRequired(false);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasShadowAuditProperties();
    }
}
