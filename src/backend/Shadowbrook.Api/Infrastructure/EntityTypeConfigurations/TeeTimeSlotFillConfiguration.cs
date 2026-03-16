using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Domain.TeeTimeRequestAggregate;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class TeeTimeSlotFillConfiguration : IEntityTypeConfiguration<TeeTimeSlotFill>
{
    public void Configure(EntityTypeBuilder<TeeTimeSlotFill> builder)
    {
        builder.ToTable("WaitlistSlotFills");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.HasIndex(f => f.BookingId);
    }
}
