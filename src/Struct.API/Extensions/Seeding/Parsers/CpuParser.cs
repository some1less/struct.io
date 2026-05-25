using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CpuParser : IComponentParser
{
    public string TargetFolderName => "CPU";

    public Category DetermineCategory(JsonNode root) => Category.Cpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        var memoryType = ts?["MemoryType"]?.ToString() ?? "Unknown";

        return new Dictionary<string, string>
        {
            { "Cores", ts?["Cores"]?.ToString() ?? "0" },
            { "Threads", ts?["Threads"]?.ToString() ?? "0" },
            { "BaseClock", ts?["BaseClock"]?.ToString() ?? "0" },
            { "BoostClock", ts?["BoostClock"]?.ToString() ?? "0" },
            { "TDP", ts?["TDP"]?.ToString() ?? "0" },
            { "Socket", ts?["Socket"]?.ToString() ?? "Unknown" },
            { "MemoryType", memoryType }
        };
    }
}