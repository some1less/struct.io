using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CpuParser : IComponentParser
{
    public string TargetFolderName => "CPU";

    public Category DetermineCategory(JsonNode root) => Category.Cpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        var memoryType = ts?[Spec.MemoryType]?.ToString() ?? "Unknown";

        return new Dictionary<string, string>
        {
            { Spec.Cores, ts?[Spec.Cores]?.ToString() ?? "0" },
            { Spec.Threads, ts?[Spec.Threads]?.ToString() ?? "0" },
            { Spec.BaseClock, ts?[Spec.BaseClock]?.ToString() ?? "0" },
            { Spec.BoostClock, ts?[Spec.BoostClock]?.ToString() ?? "0" },
            { Spec.Tdp, ts?[Spec.Tdp]?.ToString() ?? "0" },
            { Spec.Socket, ts?[Spec.Socket]?.ToString() ?? "Unknown" },
            { Spec.MemoryType, memoryType }
        };
    }
}