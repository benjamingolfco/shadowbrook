using Teeforce.Domain.Common;

namespace Teeforce.Domain.CoursePricingAggregate.Exceptions;

public class PriceOutOfBoundsException(decimal price, decimal? minPrice, decimal? maxPrice)
    : DomainException($"Price {price} is outside the allowed range [{minPrice?.ToString() ?? "∞"}, {maxPrice?.ToString() ?? "∞"}].")
{
    public decimal Price { get; } = price;
    public decimal? MinPrice { get; } = minPrice;
    public decimal? MaxPrice { get; } = maxPrice;
}
