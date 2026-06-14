using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>
/// Per-purpose weight matrix and fill sequences, shared by the greedy engine and the exact solver
/// so both optimize an identical objective. Centralized here as the single tunable table the thesis
/// references.
/// </summary>
public static class RecommendationProfiles
{
    // division matrix
    /* GAMING: GPU(35%) CPU(22%) Motherboard(12%) ...
       WORK: CPU(35%) GPU(15%) RAM(18%) ...
       OFFICE: CPU(25%) Motherboard(15%) RAM(15%) ... gpu minimum */
    public static readonly Dictionary<string, Dictionary<Category, double>> Weights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gaming"] = new()
        {
            { Category.Gpu, 0.35 }, { Category.Cpu, 0.22 }, { Category.Motherboard, 0.12 },
            { Category.Ram, 0.10 }, { Category.Ssd, 0.07 }, { Category.Psu, 0.06 },
            { Category.Case, 0.05 }, { Category.Cooler, 0.03 }
        },
        ["Work"] = new()
        {
            { Category.Cpu, 0.35 }, { Category.Gpu, 0.15 }, { Category.Ram, 0.18 },
            { Category.Motherboard, 0.12 }, { Category.Ssd, 0.08 }, { Category.Psu, 0.05 },
            { Category.Case, 0.04 }, { Category.Cooler, 0.03 }
        },
        ["Office"] = new()
        {
            { Category.Cpu, 0.25 }, { Category.Motherboard, 0.15 }, { Category.Ram, 0.15 },
            { Category.Ssd, 0.15 }, { Category.Gpu, 0.10 }, { Category.Psu, 0.08 },
            { Category.Case, 0.07 }, { Category.Cooler, 0.05 }
        }
    };

    // order of placing
    public static readonly Dictionary<string, List<Category>> Sequences = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Gaming"] = new() { Category.Gpu, Category.Cpu, Category.Motherboard,
            Category.Ram, Category.Psu, Category.Ssd,
            Category.Case, Category.Cooler },

        ["Work"] = new() { Category.Cpu, Category.Gpu, Category.Motherboard,
            Category.Ram, Category.Psu, Category.Ssd,
            Category.Case, Category.Cooler },

        ["Office"] = new() { Category.Cpu, Category.Motherboard, Category.Ram,
            Category.Ssd, Category.Psu, Category.Case,
            Category.Cooler, Category.Gpu }
    };
}
