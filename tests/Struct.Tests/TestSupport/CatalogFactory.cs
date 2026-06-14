using Struct.DAL.Models;

namespace Struct.Tests.TestSupport;

/// <summary>
/// A small but internally-coherent synthetic catalog: exactly one realistic, mutually-compatible
/// option per category for an AM5 / DDR5 build, so the recommendation engine can produce a full
/// 8-slot build deterministically.
/// </summary>
public static class CatalogFactory
{
    public static List<Component> FullCoherentCatalog() => new()
    {
        ComponentBuilder.New(Category.Cpu, "AMD Ryzen 7 7800X3D")
            .Brand("AMD").Price(1800)
            .Spec("Socket", "AM5").Spec("MemoryType", "DDR5").Spec("TDP", "120")
            .Spec("Cores", "8").Spec("Threads", "16").Spec("BaseClock", "4.2").Spec("BoostClock", "5.0"),

        ComponentBuilder.New(Category.Gpu, "NVIDIA RTX 4070")
            .Brand("NVIDIA").Price(2600)
            .Spec("VRAM", "12").Spec("CoreClock", "1920").Spec("TDP", "200").Spec("Length", "240"),

        ComponentBuilder.New(Category.Motherboard, "MSI B650 Tomahawk")
            .Brand("MSI").Price(800)
            .Spec("Socket", "AM5").Spec("RamType", "DDR5").Spec("FormFactor", "ATX")
            .Spec("RamSlots", "4").Spec("MaxRam", "128"),

        ComponentBuilder.New(Category.Ram, "Corsair Vengeance 32GB DDR5")
            .Brand("Corsair").Price(500)
            .Spec("Type", "DDR5").Spec("Capacity", "32").Spec("Speed", "6000").Spec("Modules", "2"),

        ComponentBuilder.New(Category.Psu, "Corsair RM750e")
            .Brand("Corsair").Price(450)
            .Spec("Wattage", "750").Spec("Efficiency", "Gold").Spec("Modular", "Full"),

        ComponentBuilder.New(Category.Ssd, "Samsung 990 Pro 1TB")
            .Brand("Samsung").Price(400)
            .Spec("Capacity", "1 TB").Spec("Interface", "NVMe"),

        ComponentBuilder.New(Category.Case, "Fractal North ATX")
            .Brand("Fractal").Price(400)
            .Spec("SupportedMotherboards", "ATX, Micro-ATX, Mini-ITX")
            .Spec("MaxGpuLength", "360").Spec("MaxCoolerHeight", "170"),

        ComponentBuilder.New(Category.Cooler, "Thermalright Peerless Assassin")
            .Brand("Thermalright").Price(300)
            .Spec("Height", "155").Spec("CpuSockets", "AM5, AM4, LGA1700"),
    };

    /// <summary>Same catalog with every component of <paramref name="missing"/> removed.</summary>
    public static List<Component> WithoutCategory(Category missing) =>
        FullCoherentCatalog().Where(c => c.Category != missing).ToList();
}
