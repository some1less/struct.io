using System.Globalization;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Scoring;

public class PerformanceScorer : IPerformanceScorer
{
    public double CalculateScore(Component component, string purpose = "Gaming")
    {
        if (component.TechnicalSpecs == null) return 0.0;

        string lowerName = component.Name.ToLowerInvariant();
        bool isWork = purpose.Equals("Work", StringComparison.OrdinalIgnoreCase);

        return component.Category switch
        {
            Category.Cpu => ScoreCpu(component.TechnicalSpecs, lowerName, isWork),
            Category.Gpu => ScoreGpu(component.TechnicalSpecs, lowerName, isWork),
            Category.Motherboard => ScoreMotherboard(component.TechnicalSpecs, lowerName),
            Category.Ram => ScoreRam(component.TechnicalSpecs, lowerName),
            Category.Psu => ScorePsu(component.TechnicalSpecs, lowerName),
            Category.Ssd => ScoreStorage(component.TechnicalSpecs, true),
            Category.Hdd => ScoreStorage(component.TechnicalSpecs, false),
            Category.Case => ScoreCase(component.TechnicalSpecs, lowerName),
            Category.Cooler => ScoreCooler(component.TechnicalSpecs, lowerName),
            _ => 0.0
        };
    }

    private double ScoreCpu(Dictionary<string, string> specs, string name, bool isWork)
    {
        double cores = ParseDouble(specs, "Cores", 4);
        double threads = ParseDouble(specs, "Threads", 4);
        double baseClock = ParseDouble(specs, "BaseClock", 3.0);
        double boostClock = ParseDouble(specs, "BoostClock", 4.0);

        double effectiveCores = isWork ? cores : Math.Min(cores, 8);
        double effectiveThreads = isWork ? threads : Math.Min(threads, 16);

        double coreWeight = isWork ? 20 : 10;
        double threadWeight = isWork ? 6 : 3;
        double clockWeight = isWork ? 30 : 60;

        double rawScore = (effectiveCores * coreWeight) + (effectiveThreads * threadWeight) + 
                          (baseClock * clockWeight) + (boostClock * clockWeight * 1.5);
        
        if (name.Contains("x3d")) rawScore += 300; 

        if (!isWork && (name.Contains("threadripper") || name.Contains("xeon") || name.Contains("epyc")))
            rawScore *= 0.2;
        else if (isWork && (name.Contains("threadripper") || name.Contains("xeon w")))
            rawScore += 500;

        return Normalize(rawScore, 150, 3500);
    }

    private double ScoreGpu(Dictionary<string, string> specs, string name, bool isWork)
    {
        double vram = ParseDouble(specs, "VRAM", 4);
        double coreClock = ParseDouble(specs, "CoreClock", 1000);

        double effectiveVram = isWork ? vram : Math.Min(vram, 24);

        double tierBonus = 0;
        if (name.Contains("4090") || name.Contains("7900 xtx") || name.Contains("5090")) tierBonus = 1200;
        else if (name.Contains("4080") || name.Contains("7900 xt") || name.Contains("5080")) tierBonus = 800;
        else if (name.Contains("4070") || name.Contains("7800") || name.Contains("7700")) tierBonus = 500;
        else if (name.Contains("4060") || name.Contains("7600") || name.Contains("3060")) tierBonus = 250;

        double rawScore = (effectiveVram * 40) + (coreClock * 0.1) + tierBonus;

        if (!isWork && (name.Contains("pro w") || name.Contains("quadro") || name.Contains("rtx a") || name.Contains("ai top")))
            rawScore *= 0.2;

        return Normalize(rawScore, 200, 2500);
    }

    private double ScoreMotherboard(Dictionary<string, string> specs, string name)
    {
        double ramSlots = ParseDouble(specs, "RamSlots", 2);
        double maxRam = ParseDouble(specs, "MaxRam", 32);
        
        double tierBonus = 0;
        if (name.Contains("x670") || name.Contains("z790") || name.Contains("x870")) tierBonus = 300;
        else if (name.Contains("b650") || name.Contains("b760")) tierBonus = 150;

        double rawScore = tierBonus + (ramSlots * 20) + (maxRam * 0.5);
        return Normalize(rawScore, 50, 600);
    }

    private double ScoreRam(Dictionary<string, string> specs, string name)
    {
        double capacity = ParseDouble(specs, "Capacity", 8);
        double speed = ParseDouble(specs, "Speed", 2400);
        
        string type = specs.TryGetValue("Type", out var t) ? t.ToUpperInvariant() : "";
        double typeBonus = type.Contains("DDR5") ? 200 : type.Contains("DDR4") ? 100 : 20;

        double rawScore = (capacity * 8) + (speed * 0.05) + typeBonus;
        return Normalize(rawScore, 100, 1200);
    }

    private double ScorePsu(Dictionary<string, string> specs, string name)
    {
        double wattage = ParseDouble(specs, "Wattage", 500);
        
        double effBonus = 0;
        string eff = specs.TryGetValue("Efficiency", out var e) ? e.ToLowerInvariant() : "";
        if (eff.Contains("platinum") || eff.Contains("titanium")) effBonus = 150;
        else if (eff.Contains("gold")) effBonus = 80;
        else if (eff.Contains("bronze")) effBonus = 30;

        double rawScore = wattage + effBonus;
        return Normalize(rawScore, 300, 1800);
    }

    private double ScoreStorage(Dictionary<string, string> specs, bool isSsd)
    {
        double capacity = ParseDouble(specs, "Capacity", 500);
        if (specs.TryGetValue("Capacity", out var rawCap) && rawCap.ToUpperInvariant().Contains("TB")) 
            capacity *= 1000;

        string iface = specs.TryGetValue("Interface", out var i) ? i.ToLowerInvariant() : "";
        bool isNvme = iface.Contains("nvme") || iface.Contains("pcie") || iface.Contains("m.2");

        double rawScore = (capacity * 0.1) + (isSsd ? 100 : 0) + (isNvme ? 150 : 0);
        return Normalize(rawScore, 50, 1000);
    }

    private double ScoreCase(Dictionary<string, string> specs, string name)
    {
        double maxGpu = ParseDouble(specs, "MaxGpuLength", 300);
        double rawScore = maxGpu + (name.Contains("glass") ? 50 : 0) + (name.Contains("mesh") ? 30 : 0);
        return Normalize(rawScore, 200, 600);
    }

    private double ScoreCooler(Dictionary<string, string> specs, string name)
    {
        bool isWater = specs.TryGetValue("WaterCooled", out var w) && w.Equals("True", StringComparison.OrdinalIgnoreCase);
        double radSize = ParseDouble(specs, "RadiatorSize", 0);
        
        double rawScore = isWater ? (200 + radSize) : 100;
        if (name.Contains("noctua") || name.Contains("assassin") || name.Contains("dark rock")) rawScore += 100;
        if (name.Contains("lcd") || name.Contains("ryujin")) rawScore += 150;

        return Normalize(rawScore, 50, 800);
    }
    

    private double ParseDouble(Dictionary<string, string> specs, string key, double fallback)
    {
        if (!specs.TryGetValue(key, out var val)) return fallback;
        
        // ex: 3.5 GHz or 3,5 GHz
        var cleanVal = new string(val.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        cleanVal = cleanVal.Replace(',', '.');

        return double.TryParse(cleanVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) 
            ? result 
            : fallback;
    }

    // takes input value and transforms to from 0.0 to 1.0
    private double Normalize(double value, double min, double max)
    {
        double normalized = (value - min) / (max - min);
        return Math.Clamp(normalized, 0.0, 1.0);
    }
}