using Struct.BLL.Core.Compatibility;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

/// <summary>
/// Characterization tests pinning the CompatibilityEngine's current behavior. The "✅ verified
/// correct" cases mirror the manual review (Feedback - Compatibility Engine); the KnownGap cases
/// pin today's (incorrect) behavior so a Phase-2 fix will deliberately flip them.
/// </summary>
public class CompatibilityEngineTests
{
    private readonly CompatibilityEngine _engine = new();

    // ---- Socket ---------------------------------------------------------

    [Fact]
    public void Socket_Am5Cpu_With_Am4Motherboard_IsRejected()
    {
        var build = new BuildContext { Cpu = ComponentBuilder.New(Category.Cpu, "Ryzen 7 7800X3D").Spec("Socket", "AM5") };
        Component mobo = ComponentBuilder.New(Category.Motherboard, "B450 Tomahawk").Spec("Socket", "AM4");

        var result = _engine.CheckCompatibility(build, mobo);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Violations, v => v.Contains("Socket mismatch"));
    }

    [Fact]
    public void Socket_Am5Cpu_With_Am5Motherboard_IsAccepted()
    {
        var build = new BuildContext { Cpu = ComponentBuilder.New(Category.Cpu, "Ryzen 7 7800X3D").Spec("Socket", "AM5") };
        Component mobo = ComponentBuilder.New(Category.Motherboard, "B650 Tomahawk").Spec("Socket", "AM5");

        Assert.True(_engine.CheckCompatibility(build, mobo).IsCompatible);
    }

    // ---- Memory ---------------------------------------------------------

    [Fact]
    public void Memory_Ddr4Ram_On_Lga1700CpuTaggedDdr5_IsAccepted_ViaDualControllerException()
    {
        var build = new BuildContext
        {
            Cpu = ComponentBuilder.New(Category.Cpu, "Core i5-12600K")
                .Spec("Socket", "LGA1700").Spec("MemoryType", "DDR5")
        };
        Component ram = ComponentBuilder.New(Category.Ram, "Corsair Vengeance 32GB").Spec("Type", "DDR4");

        Assert.True(_engine.CheckCompatibility(build, ram).IsCompatible);
    }

    [Fact]
    public void Memory_Ddr4Ram_With_Ddr5Motherboard_IsRejected()
    {
        var build = new BuildContext
        {
            Motherboard = ComponentBuilder.New(Category.Motherboard, "B650").Spec("RamType", "DDR5")
        };
        Component ram = ComponentBuilder.New(Category.Ram, "Corsair Vengeance 32GB").Spec("Type", "DDR4");

        var result = _engine.CheckCompatibility(build, ram);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Violations, v => v.Contains("Memory mismatch"));
    }

    [Fact]
    public void Ram_ModuleCount_ExceedingMotherboardSlots_IsRejected()
    {
        // Phase 2: a 4-stick kit cannot fit a 2-slot board.
        var build = new BuildContext
        {
            Motherboard = ComponentBuilder.New(Category.Motherboard, "B650I ITX").Spec("RamSlots", "2")
        };
        Component ram = ComponentBuilder.New(Category.Ram, "Corsair 4x16GB")
            .Spec("Modules", "4").Spec("Capacity", "64");

        var result = _engine.CheckCompatibility(build, ram);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Violations, v => v.Contains("slot"));
    }

    [Fact]
    public void Ram_TotalCapacity_ExceedingMotherboardMax_IsRejected()
    {
        var build = new BuildContext
        {
            Motherboard = ComponentBuilder.New(Category.Motherboard, "A320M").Spec("MaxRam", "32").Spec("RamSlots", "2")
        };
        Component ram = ComponentBuilder.New(Category.Ram, "Corsair 2x64GB")
            .Spec("Modules", "2").Spec("Capacity", "128");

        var result = _engine.CheckCompatibility(build, ram);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Violations, v => v.Contains("capacity"));
    }

    [Fact]
    public void Ram_FittingSlotsAndCapacity_IsAccepted()
    {
        var build = new BuildContext
        {
            Motherboard = ComponentBuilder.New(Category.Motherboard, "B650 Tomahawk").Spec("RamSlots", "4").Spec("MaxRam", "128")
        };
        Component ram = ComponentBuilder.New(Category.Ram, "Corsair 2x16GB")
            .Spec("Modules", "2").Spec("Capacity", "32");

        Assert.True(_engine.CheckCompatibility(build, ram).IsCompatible);
    }

    // ---- Power ----------------------------------------------------------

    [Fact]
    public void Power_HighEndCpuGpu_With_UndersizedPsu_IsRejected()
    {
        var build = new BuildContext
        {
            Cpu = ComponentBuilder.New(Category.Cpu, "Core i9-14900K").Spec("TDP", "253"),
            Gpu = ComponentBuilder.New(Category.Gpu, "RTX 4090").Spec("TDP", "450")
        };
        Component psu = ComponentBuilder.New(Category.Psu, "Corsair 450W").Spec("Wattage", "450");

        var result = _engine.CheckCompatibility(build, psu);

        Assert.False(result.IsCompatible);
        // Pins the formula: (253 + 450 + 50) * 1.3 = 978.9 -> ceil 979
        Assert.Contains(result.Violations, v => v.Contains("979"));
    }

    [Fact]
    public void Power_AmpleWattage_IsAccepted()
    {
        var build = new BuildContext
        {
            Cpu = ComponentBuilder.New(Category.Cpu, "Ryzen 7 7800X3D").Spec("TDP", "120"),
            Gpu = ComponentBuilder.New(Category.Gpu, "RTX 4070").Spec("TDP", "200")
        };
        Component psu = ComponentBuilder.New(Category.Psu, "Corsair 750W").Spec("Wattage", "750");

        Assert.True(_engine.CheckCompatibility(build, psu).IsCompatible);
    }

    // ---- Physical clearance --------------------------------------------

    [Fact]
    public void Clearance_EatxBoard_In_AtxCase_IsAllowed_IntentionalDataWorkaround()
    {
        var build = new BuildContext
        {
            Motherboard = ComponentBuilder.New(Category.Motherboard, "X670E Extreme").Spec("FormFactor", "EATX")
        };
        Component pcCase = ComponentBuilder.New(Category.Case, "Lian Li O11")
            .Spec("SupportedMotherboards", "ATX, Micro-ATX, Mini-ITX");

        // Documented hack: EATX boards are allowed into any ATX case to avoid bricking high-end builds.
        Assert.True(_engine.CheckCompatibility(build, pcCase).IsCompatible);
    }

    [Fact]
    public void Clearance_GpuLongerThanCase_IsRejected()
    {
        var build = new BuildContext
        {
            Gpu = ComponentBuilder.New(Category.Gpu, "RTX 4090 Gaming OC").Spec("Length", "350")
        };
        Component pcCase = ComponentBuilder.New(Category.Case, "Tiny ITX")
            .Spec("MaxGpuLength", "300").Spec("SupportedMotherboards", "Mini-ITX");

        var result = _engine.CheckCompatibility(build, pcCase);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Violations, v => v.Contains("Clearance issue"));
    }

    // ---- KNOWN GAPS (pin current behavior; Phase-2 fixes will flip these) ----

    [Fact]
    public void CoolerHeight_ExceedingCaseClearance_IsRejected()
    {
        // Feedback #5 (Phase 2 fix): a 165 mm tower cooler must not fit a 77 mm ITX case.
        var build = new BuildContext
        {
            Case = ComponentBuilder.New(Category.Case, "Tiny ITX")
                .Spec("MaxCoolerHeight", "77").Spec("SupportedMotherboards", "Mini-ITX")
        };
        Component cooler = ComponentBuilder.New(Category.Cooler, "Noctua NH-D15")
            .Spec("Height", "165").Spec("CpuSockets", "AM5, AM4, LGA1700");

        var result = _engine.CheckCompatibility(build, cooler);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Violations, v => v.Contains("Cooler height"));
    }

    [Fact]
    public void CoolerHeight_WithinCaseClearance_IsAccepted()
    {
        var build = new BuildContext
        {
            Case = ComponentBuilder.New(Category.Case, "Mid Tower")
                .Spec("MaxCoolerHeight", "170").Spec("SupportedMotherboards", "ATX")
        };
        Component cooler = ComponentBuilder.New(Category.Cooler, "Peerless Assassin")
            .Spec("Height", "155").Spec("CpuSockets", "AM5, AM4, LGA1700");

        Assert.True(_engine.CheckCompatibility(build, cooler).IsCompatible);
    }

    [Fact]
    public void CoolerHeight_MissingCaseSpec_SilentlyPasses()
    {
        // Consistent with the engine's dirty-data philosophy: no MaxCoolerHeight => skip the check.
        var build = new BuildContext
        {
            Case = ComponentBuilder.New(Category.Case, "Mystery Case").Spec("SupportedMotherboards", "ATX")
        };
        Component cooler = ComponentBuilder.New(Category.Cooler, "Noctua NH-D15").Spec("Height", "165");

        Assert.True(_engine.CheckCompatibility(build, cooler).IsCompatible);
    }

    [Fact]
    public void MissingSpec_ResultsIn_SilentPass()
    {
        // Design choice for dirty data: a missing Socket means the check is skipped entirely.
        var build = new BuildContext { Cpu = ComponentBuilder.New(Category.Cpu, "Mystery CPU") };
        Component mobo = ComponentBuilder.New(Category.Motherboard, "Some Board").Spec("Socket", "AM5");

        Assert.True(_engine.CheckCompatibility(build, mobo).IsCompatible);
    }
}
