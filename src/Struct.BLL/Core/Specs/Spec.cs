namespace Struct.BLL.Core.Specs;

/// <summary>
/// Keys of <c>Component.TechnicalSpecs</c> — and the column names of the catalog JSON. The only place
/// these strings may appear: a typo elsewhere would make a compatibility rule silently pass.
/// </summary>
public static class Spec
{
    // CPU
    public const string Socket = "Socket";
    public const string MemoryType = "MemoryType";
    public const string Cores = "Cores";
    public const string Threads = "Threads";
    public const string BaseClock = "BaseClock";
    public const string BoostClock = "BoostClock";
    public const string Tdp = "TDP";

    // GPU
    public const string Vram = "VRAM";
    public const string CoreClock = "CoreClock";
    public const string Length = "Length";
    public const string Chipset = "Chipset";

    // Motherboard
    public const string RamType = "RamType";
    public const string RamSlots = "RamSlots";
    public const string MaxRam = "MaxRam";

    // RAM / storage
    public const string Type = "Type";
    public const string Capacity = "Capacity";
    public const string Speed = "Speed";
    public const string Modules = "Modules";
    public const string Interface = "Interface";

    // PSU
    public const string Wattage = "Wattage";
    public const string Efficiency = "Efficiency";
    public const string Modular = "Modular";

    // Case
    public const string MaxGpuLength = "MaxGpuLength";
    public const string MaxCoolerHeight = "MaxCoolerHeight";
    public const string SupportedMotherboards = "SupportedMotherboards";
    public const string SidePanel = "SidePanel";

    // Cooler
    public const string CpuSockets = "CpuSockets";
    public const string Height = "Height";
    public const string WaterCooled = "WaterCooled";
    public const string RadiatorSize = "RadiatorSize";

    // shared
    public const string FormFactor = "FormFactor";

    /// <summary>Per-category 0..1 PassMark ratio written at seed time by <c>BenchmarkScores</c>.</summary>
    public const string BenchmarkScore = "BenchmarkScore";
}
