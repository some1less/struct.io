using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CoolerParser : IComponentParser
{
    public string TargetFolderName => "CPUCooler";

    public Category DetermineCategory(JsonNode root) => Category.Cooler;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { Spec.CpuSockets, ts?[Spec.CpuSockets]?.ToString() ?? "Unknown" },
            { Spec.Height, ts?[Spec.Height]?.ToString() ?? "0" },
            { Spec.WaterCooled, ts?[Spec.WaterCooled]?.ToString() ?? "False" },
            { Spec.RadiatorSize, ts?[Spec.RadiatorSize]?.ToString() ?? "0" }
        };
    }
}