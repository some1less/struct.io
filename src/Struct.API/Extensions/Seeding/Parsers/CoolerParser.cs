using System.Text.Json.Nodes;
using Struct.DAL.Models;

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
            { "CpuSockets", ts?["CpuSockets"]?.ToString() ?? "Unknown" },
            { "Height", ts?["Height"]?.ToString() ?? "0" },
            { "WaterCooled", ts?["WaterCooled"]?.ToString() ?? "False" },
            { "RadiatorSize", ts?["RadiatorSize"]?.ToString() ?? "0" }
        };
    }
}