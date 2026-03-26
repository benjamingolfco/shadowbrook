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
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

        builder.ComplexProperty(b => b.TeeTime, t =>
        {
            t.Property(x => x.Date).HasColumnName("Date");
            t.Property(x => x.Time).HasColumnName("TeeTime").HasColumnType("time");
        });

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();

        builder.HasIndex(o => o.CourseId);
    }
}
