namespace Struct.BLL.Core.Compatibility;

/// <summary>Required PSU wattage = (CPU TDP + GPU TDP + PsuBaseLoadWatts) × PsuHeadroom.</summary>
public record CompatibilitySettings
{
    public double PsuBaseLoadWatts { get; init; }
    public double PsuHeadroom { get; init; }
}
