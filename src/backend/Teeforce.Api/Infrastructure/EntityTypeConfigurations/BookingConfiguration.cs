using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.BookingAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.TeeTimeId);
        builder.Property(b => b.PricePerPlayer).HasPrecision(18, 2);
        builder.Property(b => b.TotalPrice).HasPrecision(18, 2);

        builder.ComplexProperty(b => b.TeeTime, t =>
            t.Property(x => x.Value).HasColumnName("TeeTime").HasColumnType("datetime2"));

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(o => o.CourseId);
        builder.HasIndex(b => b.TeeTimeId);
    }
}
