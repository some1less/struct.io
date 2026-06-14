using Struct.BLL.Core.Compatibility;
using Struct.DAL.Models;

namespace Struct.BLL.Core.Recommendation;

/// <summary>Shared build-context operations used by the greedy engine and the exact solver.</summary>
public static class BuildContextOps
{
    public static BuildContext Clone(BuildContext b) => new()
    {
        Cpu = b.Cpu, Gpu = b.Gpu, Motherboard = b.Motherboard, Ram = b.Ram,
        Psu = b.Psu, Case = b.Case, Cooler = b.Cooler, Storage = b.Storage
    };

    public static void Assign(BuildContext ctx, Component candidate)
    {
        switch (candidate.Category)
        {
            case Category.Cpu: ctx.Cpu = candidate; break;
            case Category.Gpu: ctx.Gpu = candidate; break;
            case Category.Motherboard: ctx.Motherboard = candidate; break;
            case Category.Ram: ctx.Ram = candidate; break;
            case Category.Psu: ctx.Psu = candidate; break;
            case Category.Case: ctx.Case = candidate; break;
            case Category.Cooler: ctx.Cooler = candidate; break;
            case Category.Ssd:
            case Category.Hdd: ctx.Storage = candidate; break;
        }
    }

    public static Component? Get(BuildContext b, Category c) => c switch
    {
        Category.Cpu => b.Cpu,
        Category.Gpu => b.Gpu,
        Category.Motherboard => b.Motherboard,
        Category.Ram => b.Ram,
        Category.Psu => b.Psu,
        Category.Case => b.Case,
        Category.Cooler => b.Cooler,
        Category.Ssd or Category.Hdd => b.Storage,
        _ => null
    };
}
