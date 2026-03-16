using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.BookingAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.GolferName).IsRequired().HasMaxLength(200);

        builder.HasIndex(b => new { b.CourseId, b.Date, b.Time });
    }
}
