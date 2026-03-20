using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shadowbrook.Api.Infrastructure.EntityTypeConfigurations;

public static class EntityTypeBuilderExtensions
{
    public static EntityTypeBuilder<T> HasShadowRowVersion<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        return builder;
    }

    public static EntityTypeBuilder<T> HasShadowAuditProperties<T>(this EntityTypeBuilder<T> builder) where T : class
    {
        builder.Property<DateTimeOffset>("UpdatedAt");
        builder.Property<string?>("UpdatedBy").HasMaxLength(200);
        return builder;
    }
}
