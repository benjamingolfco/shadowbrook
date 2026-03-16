using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shadowbrook.Api.Models;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public class WaitlistRequestAcceptanceConfiguration : IEntityTypeConfiguration<WaitlistRequestAcceptance>
{
    public void Configure(EntityTypeBuilder<WaitlistRequestAcceptance> builder)
    {
        builder.ToTable("WaitlistRequestAcceptances");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasIndex(a => new { a.WaitlistRequestId, a.GolferWaitlistEntryId }).IsUnique();
        builder.HasIndex(a => a.WaitlistOfferId);
    }
}
