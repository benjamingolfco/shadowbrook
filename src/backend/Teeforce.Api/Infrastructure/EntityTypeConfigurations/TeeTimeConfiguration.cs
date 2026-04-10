using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeTimeAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeConfiguration : IEntityTypeConfiguration<TeeTime>
{
    public void Configure(EntityTypeBuilder<TeeTime> builder)
    {
        builder.ToTable("TeeTimes");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TeeSheetId).IsRequired();
        builder.Property(t => t.TeeSheetIntervalId).IsRequired();
        builder.Property(t => t.CourseId).IsRequired();
        builder.Property(t => t.Date).IsRequired();
        builder.Property(t => t.Time).HasColumnType("time");
        builder.Property(t => t.Capacity);
        builder.Property(t => t.Remaining);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.CreatedAt);

        builder.OwnsMany(t => t.Claims, c =>
        {
            c.ToTable("TeeTimeClaims");
            c.WithOwner().HasForeignKey(x => x.TeeTimeId);
            c.HasKey(x => x.Id);
            c.Property(x => x.Id).ValueGeneratedNever();
            c.Property(x => x.BookingId);
            c.Property(x => x.GolferId);
            c.Property(x => x.GroupSize);
            c.Property(x => x.ClaimedAt);
            c.HasIndex(x => new { x.TeeTimeId, x.BookingId }).IsUnique();
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(t => t.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TeeSheetIntervalId).IsUnique();
        builder.HasIndex(t => new { t.CourseId, t.Date });

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
