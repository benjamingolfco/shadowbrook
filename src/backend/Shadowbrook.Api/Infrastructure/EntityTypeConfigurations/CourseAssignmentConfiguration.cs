using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.AppUserAggregate;
using Shadowbrook.Domain.CourseAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class CourseAssignmentConfiguration : IEntityTypeConfiguration<CourseAssignment>
{
    public void Configure(EntityTypeBuilder<CourseAssignment> builder)
    {
        builder.ToTable("CourseAssignments");
        builder.HasKey(ca => ca.Id);
        builder.Property(ca => ca.Id).ValueGeneratedNever();

        builder.HasIndex(ca => new { ca.AppUserId, ca.CourseId }).IsUnique();
        builder.Property(ca => ca.AssignedAt);

        builder.HasOne<AppUser>()
            .WithMany(u => u.CourseAssignments)
            .HasForeignKey(ca => ca.AppUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(ca => ca.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
