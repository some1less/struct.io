using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CoolerParser : IComponentParser
{
    public string TargetFolderName => "CPUCooler";

    public Category DetermineCategory(JsonNode root) => Category.Cooler;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var socketsNode = root["cpu_sockets"]?.AsArray();
        var sockets = socketsNode != null
            ? string.Join(",", socketsNode.Select(x => x?.ToString()))
            : "Unknown";

        return new Dictionary<string, string>
        {
            { "CpuSockets", sockets },
            { "Height", root["height"]?.ToString() ?? "0" },
            { "WaterCooled", root["water_cooled"]?.ToString() ?? "False" },
            { "RadiatorSize", root["radiator_size"]?.ToString() ?? "0" }
        };
    }
}