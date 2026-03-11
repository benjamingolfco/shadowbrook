using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.GolferAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class GolferConfiguration : IEntityTypeConfiguration<Golfer>
{
    public void Configure(EntityTypeBuilder<Golfer> builder)
    {
        builder.ToTable("Golfers");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.Phone).IsRequired().HasMaxLength(20);
        builder.Property(g => g.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(g => g.LastName).IsRequired().HasMaxLength(100);

        builder.HasIndex(g => g.Phone).IsUnique();
    }
}
