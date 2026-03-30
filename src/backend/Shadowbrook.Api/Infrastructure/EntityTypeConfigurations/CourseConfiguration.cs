using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.CourseAggregate;
using Shadowbrook.Domain.OrganizationAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.TimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(c => c.StreetAddress).HasMaxLength(200);
        builder.Property(c => c.City).HasMaxLength(100);
        builder.Property(c => c.State).HasMaxLength(100);
        builder.Property(c => c.ZipCode).HasMaxLength(20);
        builder.Property(c => c.ContactEmail).HasMaxLength(200);
        builder.Property(c => c.ContactPhone).HasMaxLength(20);
        builder.Property(c => c.FlatRatePrice).HasPrecision(18, 2);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(c => c.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.OrganizationId);
        builder.HasIndex(c => new { c.OrganizationId, c.Name }).IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
