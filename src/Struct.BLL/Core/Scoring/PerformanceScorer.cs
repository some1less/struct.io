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
            Category.Ram => ScoreRam(component.TechnicalSpecs, lowerName, isWork),
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
        string socket = specs.TryGetValue("Socket", out var s) ? s.ToUpperInvariant() : "";

        double effectiveCores = isWork ? cores : Math.Min(cores, 8);
        double effectiveThreads = isWork ? threads : Math.Min(threads, 16);

        // Reduced clock weights, increased core/thread weights
        double coreWeight = isWork ? 30 : 20;
        double threadWeight = isWork ? 10 : 5;
        double baseClockWeight = isWork ? 5 : 5; // drastically reduced
        double boostClockWeight = isWork ? 15 : 25; // boost clock is better for gaming

        double rawScore = (effectiveCores * coreWeight) + (effectiveThreads * threadWeight) + 
                          (baseClock * baseClockWeight) + (boostClock * boostClockWeight);
        
        // Generational IPC scaling based on Socket
        double genMultiplier = 0.8; // Old defaults
        if (socket.Contains("1700") || socket.Contains("AM5") || socket.Contains("1851")) genMultiplier = 1.3;
        else if (socket.Contains("1200") || socket.Contains("AM4")) genMultiplier = 1.0;
        
        rawScore *= genMultiplier;

        if (name.Contains("x3d")) rawScore += 250; 

        if (!isWork && (name.Contains("threadripper") || name.Contains("xeon") || name.Contains("epyc")))
            rawScore *= 0.2;
        else if (isWork && (name.Contains("threadripper") || name.Contains("xeon w")))
            rawScore += 500;

        // Recalibrated max: Work builds can reach ~3500+ (Threadrippers), Gaming builds cap around ~850 (8 cores max + IPC + Clocks)
        double maxScore = isWork ? 4000 : 850;
        return Normalize(rawScore, 100, maxScore);
    }

    private double ScoreGpu(Dictionary<string, string> specs, string name, bool isWork)
    {
        double vram = ParseDouble(specs, "VRAM", 4);
        if (vram == 0) vram = 4; // Fallback for strict minimum VRAM
        double coreClock = Math.Min(ParseDouble(specs, "CoreClock", 1000), 3000);

        double effectiveVram = isWork ? vram : Math.Min(vram, 24);

        double tierBonus = 0;
        // Flagship tier
        if (name.Contains(" 4090") || name.Contains(" 5090") || name.Contains("7900 xtx") || name.Contains("3090 ti") || name.Contains("6950 xt")) tierBonus = 1200;
        // High-end tier
        else if (name.Contains(" 4080") || name.Contains(" 5080") || name.Contains("7900 xt") || name.Contains(" 3090") || name.Contains("6900 xt")) tierBonus = 800;
        // Upper mid-range tier
        else if (name.Contains("4070 ti") || name.Contains("7900 gre") || name.Contains("6800 xt") || name.Contains(" 3080") || name.Contains("5070 ti")) tierBonus = 600;
        else if (name.Contains(" 4070") || name.Contains(" 5070") || name.Contains(" 7800") || name.Contains(" 6800") || name.Contains(" 7700") || name.Contains(" 2080 ti") || name.Contains(" 3070 ti")) tierBonus = 500;
        // Mid-range tier
        else if (name.Contains("4060 ti") || name.Contains("6700 xt") || name.Contains("6750 xt") || name.Contains(" 3070") || name.Contains("2080 super")) tierBonus = 350;
        else if (name.Contains(" 4060") || name.Contains(" 5060") || name.Contains(" 7600") || name.Contains(" 3060") || name.Contains(" 2070") || name.Contains(" 6600 xt") || name.Contains("b580")) tierBonus = 250;
        // Budget tier
        else if (name.Contains(" 3050") || name.Contains(" 1660") || name.Contains(" 2060") || name.Contains(" 6600") || name.Contains(" 6500")) tierBonus = 100;

        // Ti / Super / XT variant bumps tier slightly if not caught explicitly above
        if (tierBonus > 0 && (name.EndsWith(" ti") || name.Contains("-ti") || name.Contains("ti super") || name.Contains(" super") || name.Contains(" xt")))
            tierBonus += 100;

        // Heavily nerfed coreClock impact (from 0.1 to 0.01) to prevent architecture speed bias
        double rawScore = (effectiveVram * 40) + (coreClock * 0.01) + tierBonus;

        if (!isWork && (name.Contains("pro w") || name.Contains("quadro") || name.Contains("rtx a") || 
                        name.Contains("ai top") || name.Contains("ai pro") || 
                        name.Contains("arc pro") || name.Contains("titan") ||
                        name.Contains("radeon pro")))
            rawScore *= 0.2;

        // Recalibrated max: top GPUs (5090/4090) reach ~2200
        return Normalize(rawScore, 200, 2400);
    }

    private double ScoreMotherboard(Dictionary<string, string> specs, string name)
    {
        double ramSlots = ParseDouble(specs, "RamSlots", 2);
        double maxRam = ParseDouble(specs, "MaxRam", 32);
        
        double tierBonus = 0;
        if (name.Contains("x670") || name.Contains("z790") || name.Contains("x870")) tierBonus = 300;
        else if (name.Contains("z690") || name.Contains("x570")) tierBonus = 200;
        else if (name.Contains("b650") || name.Contains("b760") || name.Contains("z590") || name.Contains("z490") || name.Contains("z390")) tierBonus = 150;
        else if (name.Contains("b550") || name.Contains("b460") || name.Contains("b450") || name.Contains("b365")) tierBonus = 80;

        double rawScore = tierBonus + (ramSlots * 20) + (maxRam * 0.5);
        return Normalize(rawScore, 50, 600);
    }

    private double ScoreRam(Dictionary<string, string> specs, string name, bool isWork)
    {
        double capacity = ParseDouble(specs, "Capacity", 8);
        double speed = ParseDouble(specs, "Speed", 2400);
        double modules = ParseDouble(specs, "Modules", 1);
        
        string type = specs.TryGetValue("Type", out var t) ? t.ToUpperInvariant() : "";
        
        // DDR5 bonus now scales with speed to avoid terrible DDR5 kits outscoring premium DDR4
        double typeBonus = type.Contains("DDR5") ? (speed > 4800 ? 150 + (speed - 4800) * 0.1 : 50) : type.Contains("DDR4") ? 100 : 20;

        // Reduced capacity weight from 20 to 10. Added more weight to speed.
        double rawScore = (capacity * 10) + (speed * 0.05) + typeBonus;

        // Increased dual-channel bonus from 150 to 300 to ensure 2x8GB beats 1x32GB for gaming
        if (modules >= 2) rawScore += 300;

        // Penalize unusably small capacity for Gaming
        if (!isWork && capacity < 8) rawScore *= 0.15;

        // Increased max to 2000 to prevent clamping for 64GB/128GB kits
        return Normalize(rawScore, 100, 2000);
    }

    private double ScorePsu(Dictionary<string, string> specs, string name)
    {
        double wattage = ParseDouble(specs, "Wattage", 500);
        
        // Cap wattage benefit — beyond 1000W has diminishing returns for gaming
        double effectiveWattage = Math.Min(wattage, 1000);
        
        double effMultiplier = 1.0;
        string eff = specs.TryGetValue("Efficiency", out var e) ? e.ToLowerInvariant() : "";
        if (eff.Contains("platinum") || eff.Contains("titanium")) effMultiplier = 1.3;
        else if (eff.Contains("gold")) effMultiplier = 1.15;
        else if (eff.Contains("bronze")) effMultiplier = 1.05;

        // Modular bonus
        string mod = specs.TryGetValue("Modular", out var m) ? m.ToLowerInvariant() : "";
        double modBonus = mod.Contains("full") ? 100 : mod.Contains("semi") ? 50 : 0;

        double rawScore = (effectiveWattage * effMultiplier) + modBonus;
        return Normalize(rawScore, 300, 1600);
    }

    private double ScoreStorage(Dictionary<string, string> specs, bool isSsd)
    {
        double capacity = ParseDouble(specs, "Capacity", 500);
        if (specs.TryGetValue("Capacity", out var rawCap) && rawCap.ToUpperInvariant().Contains("TB")) 
            capacity *= 1000;

        string iface = specs.TryGetValue("Interface", out var i) ? i.ToLowerInvariant() : "";
        bool isNvme = iface.Contains("nvme") || iface.Contains("pcie") || iface.Contains("m.2");

        // Cap HDD capacity points so a 4TB HDD doesn't beat an NVMe SSD for gaming
        double effectiveCapacity = isSsd ? capacity : Math.Min(capacity, 2000);

        // Increased NVMe bonus from 150 to 350
        double rawScore = (effectiveCapacity * 0.1) + (isSsd ? 100 : 0) + (isNvme ? 350 : 0);
        return Normalize(rawScore, 50, 1000);
    }

    private double ScoreCase(Dictionary<string, string> specs, string name)
    {
        double maxGpu = ParseDouble(specs, "MaxGpuLength", 300);
        // Cap MaxGpu contribution to 350 so massive server cases don't auto-win
        double effectiveGpuClearance = Math.Min(maxGpu, 350);

        double rawScore = effectiveGpuClearance + (name.Contains("glass") ? 60 : 0) + (name.Contains("mesh") ? 60 : 0);
        return Normalize(rawScore, 200, 600);
    }

    private double ScoreCooler(Dictionary<string, string> specs, string name)
    {
        bool isWater = specs.TryGetValue("WaterCooled", out var w) && w.Equals("True", StringComparison.OrdinalIgnoreCase);
        double radSize = ParseDouble(specs, "RadiatorSize", 0);
        if (isWater && radSize == 0) radSize = 240; // Fallback for missing rad size
        
        double height = ParseDouble(specs, "Height", 100);
        
        // Water: base 100 + radSize * 1.5 (240mm = 460)
        // Air: base 100 + height * 2.0 (160mm tower = 420)
        double rawScore = isWater ? (100 + radSize * 1.5) : (100 + height * 2.0);

        if (name.Contains("noctua") || name.Contains("assassin") || name.Contains("dark rock") || name.Contains("ak620") || name.Contains("phantom spirit") || name.Contains("peerless assassin")) 
            rawScore += 150;
        
        if (name.Contains("lcd") || name.Contains("ryujin") || name.Contains("kraken elite")) 
            rawScore += 100;

        return Normalize(rawScore, 100, 1000);
    }
    

    private double ParseDouble(Dictionary<string, string> specs, string key, double fallback)
    {
        if (!specs.TryGetValue(key, out var val)) return fallback;
        
        var cleanVal = new string(val.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        if (cleanVal.Count(c => c == ',' || c == '.') > 1)
        {
            char separator = cleanVal.Contains('.') ? '.' : ',';
            if (cleanVal.Contains('.') && cleanVal.Contains(',')) 
                separator = cleanVal.LastIndexOf('.') > cleanVal.LastIndexOf(',') ? '.' : ',';
            
            int sepIndex = cleanVal.LastIndexOf(separator);
            string wholePart = new string(cleanVal.Substring(0, sepIndex).Where(char.IsDigit).ToArray());
            string fracPart = new string(cleanVal.Substring(sepIndex).Where(char.IsDigit).ToArray());
            cleanVal = wholePart + "." + fracPart;
        }
        else
        {
            cleanVal = cleanVal.Replace(',', '.');
        }

        return double.TryParse(cleanVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double result) 
            ? result 
            : fallback;
    }

    // takes input value and transforms to from 0.0 to 1.0 using a curve
    private double Normalize(double value, double min, double max)
    {
        double normalized = (value - min) / (max - min);
        normalized = Math.Clamp(normalized, 0.0, 1.0);
        // Apply square root curve to inflate mid-tier scores for better UX 
        // e.g. 0.36 becomes 0.60, 0.81 becomes 0.90
        return Math.Sqrt(normalized);
    }
}