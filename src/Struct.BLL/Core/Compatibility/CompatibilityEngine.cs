using Struct.DAL.Models;

namespace Struct.BLL.Core.Compatibility;

public class CompatibilityEngine : ICompatibilityEngine
{
    public CompatibilityResult CheckCompatibility(BuildContext currentBuild, Component candidate)
    {
        var result = new CompatibilityResult();
        
        var testBuild = CloneContext(currentBuild);
        AssignCandidate(testBuild, candidate);

        // socket check (CPU <-> Motherboard <-> Cooler)
        CheckSocketCompatibility(testBuild, result);

        // ram heck (CPU <-> RAM <-> Motherboard)
        CheckRamCompatibility(testBuild, result);

        // power usage check (CPU + GPU vs PSU)
        CheckPowerCompatibility(testBuild, result);

        // sizes check (GPU <-> Case, Motherboard <-> Case)
        CheckPhysicalClearance(testBuild, result);

        return result;
    }
    
    private void CheckSocketCompatibility(BuildContext build, CompatibilityResult result)
    {
        string? cpuSocket = GetSpec(build.Cpu, "Socket");
        string? moboSocket = GetSpec(build.Motherboard, "Socket");
        string? coolerSockets = GetSpec(build.Cooler, "CpuSockets");

        // CPU vs Motherboard
        if (cpuSocket != null && moboSocket != null)
        {
            if (!CompareClean(cpuSocket, moboSocket))
            {
                result.Violations.Add($"Socket mismatch: CPU requires {cpuSocket}, but Motherboard is {moboSocket}.");
            }
        }

        // CPU vs Cooler
        if (cpuSocket != null && coolerSockets != null)
        {
            var supportedCoolerSockets = coolerSockets.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Replace(" ", "").ToUpperInvariant());
            
            if (!supportedCoolerSockets.Contains(cpuSocket.Replace(" ", "").ToUpperInvariant()))
            {
                result.Violations.Add($"Cooler incompatibility: Cooler does not support CPU socket {cpuSocket}.");
            }
        }
    }
    
    private void CheckRamCompatibility(BuildContext build, CompatibilityResult result)
    {
        string? ramType = GetSpec(build.Ram, "Type");
        string? moboRamType = GetSpec(build.Motherboard, "RamType");
        string? cpuMemType = GetSpec(build.Cpu, "MemoryType");

        // RAM vs Motherboard
        if (ramType != null && moboRamType != null)
        {
            if (!CompareClean(ramType, moboRamType))
            {
                result.Violations.Add($"Memory mismatch: RAM is {ramType}, but Motherboard supports {moboRamType}.");
            }
        }

        // RAM vs CPU (CPU memory controllers dictate supported RAM)
        if (ramType != null && cpuMemType != null && cpuMemType != "UNKNOWN")
        {
            string? cpuSocket = GetSpec(build.Cpu, "Socket");
            bool isLga1700 = cpuSocket != null && cpuSocket.Contains("1700");
            
            string cleanCpuMem = cpuMemType.Replace(" ", "").ToUpperInvariant();
            string cleanRamType = ramType.Replace(" ", "").ToUpperInvariant();

            if (!cleanCpuMem.Contains(cleanRamType))
            {
                // Special exception: LGA1700 supports both DDR4 and DDR5 memory controllers
                if (!(isLga1700 && (cleanRamType == "DDR4" || cleanRamType == "DDR5")))
                {
                    result.Violations.Add($"Memory mismatch: CPU supports {cpuMemType}, but selected RAM is {ramType}.");
                }
            }
        }

        // SODIMM vs Desktop Motherboard
        if (build.Ram != null && build.Motherboard != null)
        {
            string ramName = build.Ram.Name.ToLowerInvariant();
            string? moboForm = GetSpec(build.Motherboard, "FormFactor");

            if (ramName.Contains("sodimm") && moboForm != null &&
                !moboForm.Contains("Thin Mini-ITX", StringComparison.OrdinalIgnoreCase) &&
                !moboForm.Contains("Mini-STX", StringComparison.OrdinalIgnoreCase))
            {
                result.Violations.Add("Form factor mismatch: SODIMM RAM is not compatible with standard desktop motherboards.");
            }
        }

        // RAM module count vs Motherboard slots (skipped when either spec is missing/zero)
        if (build.Ram != null && build.Motherboard != null)
        {
            int modules = ParseInt(GetSpec(build.Ram, "Modules"), 0);
            int moboSlots = ParseInt(GetSpec(build.Motherboard, "RamSlots"), 0);

            if (modules > 0 && moboSlots > 0 && modules > moboSlots)
            {
                result.Violations.Add($"Memory slots mismatch: RAM kit has {modules} modules, but Motherboard has only {moboSlots} slots.");
            }

            // RAM total capacity vs Motherboard maximum
            int capacity = ParseInt(GetSpec(build.Ram, "Capacity"), 0);
            int moboMaxRam = ParseInt(GetSpec(build.Motherboard, "MaxRam"), 0);

            if (capacity > 0 && moboMaxRam > 0 && capacity > moboMaxRam)
            {
                result.Violations.Add($"Memory capacity mismatch: RAM is {capacity}GB, but Motherboard supports at most {moboMaxRam}GB.");
            }
        }
    }
    
    private void CheckPowerCompatibility(BuildContext build, CompatibilityResult result)
    {
        if (build.Psu == null) return;

        int psuWattage = ParseInt(GetSpec(build.Psu, "Wattage"), 0);
        int cpuTdp = ParseInt(GetSpec(build.Cpu, "TDP"), 65); // default if unknown
        int gpuTdp = ParseInt(GetSpec(build.Gpu, "TDP"), build.Gpu != null ? 200 : 0);

        // formula: (CPU + GPU + 50W on mb/ram) * 1.3 (30% bonus place)
        double requiredWattage = (cpuTdp + gpuTdp + 50) * 1.3;

        if (psuWattage < requiredWattage)
        {
            result.Violations.Add($"Insufficient power: System requires ~{Math.Ceiling(requiredWattage)}W, but PSU is {psuWattage}W.");
        }
    }
    
    private void CheckPhysicalClearance(BuildContext build, CompatibilityResult result)
    {
        if (build.Case == null) return;

        // GPU vs Case Length
        if (build.Gpu != null)
        {
            int gpuLength = ParseInt(GetSpec(build.Gpu, "Length"), 0);
            int caseMaxGpu = ParseInt(GetSpec(build.Case, "MaxGpuLength"), 999);

            if (caseMaxGpu == 0) caseMaxGpu = 999;

            if (gpuLength > caseMaxGpu)
            {
                result.Violations.Add($"Clearance issue: GPU is {gpuLength}mm long, but Case only supports up to {caseMaxGpu}mm.");
            }
        }

        // Cooler height vs Case clearance (skipped when either spec is missing)
        if (build.Cooler != null)
        {
            int coolerHeight = ParseInt(GetSpec(build.Cooler, "Height"), 0);
            int caseMaxCooler = ParseInt(GetSpec(build.Case, "MaxCoolerHeight"), 0);

            if (coolerHeight > 0 && caseMaxCooler > 0 && coolerHeight > caseMaxCooler)
            {
                result.Violations.Add($"Cooler height issue: Cooler is {coolerHeight}mm tall, but Case only supports up to {caseMaxCooler}mm.");
            }
        }

        // Motherboard Form Factor vs Case
        if (build.Motherboard != null)
        {
            string? moboForm = GetSpec(build.Motherboard, "FormFactor");
            string? caseSupported = GetSpec(build.Case, "SupportedMotherboards");

            if (moboForm != null && caseSupported != null)
            {
                var supported = caseSupported.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Replace(" ", "").ToUpperInvariant())
                    .ToList();
                
                string cleanMoboForm = moboForm.Replace(" ", "").ToUpperInvariant();
                
                if (!supported.Contains(cleanMoboForm) && !caseSupported.Replace(" ", "").ToUpperInvariant().Contains(cleanMoboForm))
                {
                    // Special case for EATX: database doesn't explicitly mark cases with EATX support, 
                    // so we allow EATX boards to fit in any ATX case to avoid bricking high-end builds.
                    if (cleanMoboForm == "EATX" && caseSupported.ToUpperInvariant().Contains("ATX"))
                    {
                        // Allow
                    }
                    else
                    {
                        result.Violations.Add($"Form Factor mismatch: Case does not support {moboForm} motherboards.");
                    }
                }
            }
        }
    }
    
    private string? GetSpec(Component? component, string key)
    {
        if (component?.TechnicalSpecs == null) return null;
        return component.TechnicalSpecs.TryGetValue(key, out var val) ? val : null;
    }
    
    private bool CompareClean(string s1, string s2)
    {
        return s1.Replace(" ", "").Equals(s2.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
    }

    private int ParseInt(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        
        var cleanValue = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        cleanValue = cleanValue.Replace(',', '.');

        if (double.TryParse(cleanValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double result))
        {
            return (int)Math.Round(result);
        }
        return fallback;
    }

    private BuildContext CloneContext(BuildContext original)
    {
        return new BuildContext
        {
            Cpu = original.Cpu,
            Gpu = original.Gpu,
            Motherboard = original.Motherboard,
            Ram = original.Ram,
            Psu = original.Psu,
            Case = original.Case,
            Cooler = original.Cooler,
            Storage = original.Storage
        };
    }

    private void AssignCandidate(BuildContext context, Component candidate)
    {
        switch (candidate.Category)
        {
            case Category.Cpu: context.Cpu = candidate; break;
            case Category.Gpu: context.Gpu = candidate; break;
            case Category.Motherboard: context.Motherboard = candidate; break;
            case Category.Ram: context.Ram = candidate; break;
            case Category.Psu: context.Psu = candidate; break;
            case Category.Case: context.Case = candidate; break;
            case Category.Cooler: context.Cooler = candidate; break;
            case Category.Ssd: 
            case Category.Hdd: context.Storage = candidate; break;
        }
    }
    
}