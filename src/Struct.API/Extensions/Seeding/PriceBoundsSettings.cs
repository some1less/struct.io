using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding;

/// <summary>Sanity clamp for garbage source prices ($0, scalper outliers); in-range real prices pass untouched.</summary>
public record PriceBoundsSettings
{
    public Dictionary<Category, PriceRange> PerCategory { get; init; } = new();
    public PriceRange Fallback { get; init; } = new();
}

public record PriceRange
{
    public decimal Min { get; init; }
    public decimal Max { get; init; }
}
