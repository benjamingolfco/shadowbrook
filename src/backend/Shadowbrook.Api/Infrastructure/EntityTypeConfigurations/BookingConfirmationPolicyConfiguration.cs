using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Features.Bookings;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class BookingConfirmationPolicyConfiguration : IEntityTypeConfiguration<BookingConfirmationPolicy>
{
    public void Configure(EntityTypeBuilder<BookingConfirmationPolicy> builder)
    {
        builder.ToTable("BookingConfirmationPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Ignore(p => p.Version);
    }
}
