using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Teeforce.Domain.CourseAggregate;
using Teeforce.Domain.TeeSheetAggregate;

namespace Teeforce.Api.Infrastructure.EntityTypeConfigurations;

public class TeeSheetConfiguration : IEntityTypeConfiguration<TeeSheet>
{
    public void Configure(EntityTypeBuilder<TeeSheet> builder)
    {
        builder.ToTable("TeeSheets");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.CourseId).IsRequired();
        builder.Property(s => s.Date).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.PublishedAt);
        builder.Property(s => s.CreatedAt);

        builder.ComplexProperty(s => s.Settings, ss =>
        {
            ss.Property(x => x.FirstTeeTime).HasColumnName("Settings_FirstTeeTime").HasColumnType("time");
            ss.Property(x => x.LastTeeTime).HasColumnName("Settings_LastTeeTime").HasColumnType("time");
            ss.Property(x => x.IntervalMinutes).HasColumnName("Settings_IntervalMinutes");
            ss.Property(x => x.DefaultCapacity).HasColumnName("Settings_DefaultCapacity");
        });

        builder.OwnsMany(s => s.Intervals, i =>
        {
            i.ToTable("TeeSheetIntervals");
            i.WithOwner().HasForeignKey(x => x.TeeSheetId);
            i.HasKey(x => x.Id);
            i.Property(x => x.Id).ValueGeneratedNever();
            i.Property(x => x.Time).HasColumnType("time");
            i.Property(x => x.Capacity);
            i.HasIndex(x => new { x.TeeSheetId, x.Time }).IsUnique();
        });

        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(s => s.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.CourseId, s.Date }).IsUnique();

        builder.HasShadowRowVersion();
        builder.HasShadowAuditProperties();
    }
}
