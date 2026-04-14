using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.CoursePricingAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class CoursePricingSettingsConfiguration : IEntityTypeConfiguration<CoursePricingSettings>
{
    public void Configure(EntityTypeBuilder<CoursePricingSettings> builder)
    {
        builder.ToTable("CoursePricingSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.CourseId).IsRequired();
        builder.Property(s => s.DefaultPrice).HasPrecision(18, 2);
        builder.Property(s => s.MinPrice).HasPrecision(18, 2);
        builder.Property(s => s.MaxPrice).HasPrecision(18, 2);
        builder.Property(s => s.CreatedAt);

        builder.OwnsMany(s => s.RateSchedules, rs =>
        {
            rs.ToTable("RateSchedules");
            rs.WithOwner().HasForeignKey(x => x.CoursePricingSettingsId);
            rs.HasKey(x => x.Id);
            rs.Property(x => x.Id).ValueGeneratedNever();
            rs.Property(x => x.Name).IsRequired().HasMaxLength(200);
            rs.Property(x => x.DaysOfWeek)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<DayOfWeek[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<DayOfWeek>())
                .HasColumnType("nvarchar(200)");
            rs.Property(x => x.StartTime).HasColumnType("time");
            rs.Property(x => x.EndTime).HasColumnType("time");
            rs.Property(x => x.Price).HasPrecision(18, 2);
            rs.Property(x => x.InvalidReason).HasMaxLength(500);
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CourseId).IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
