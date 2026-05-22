using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class CpuParser : IComponentParser
{
    public string TargetFolderName => "CPU";

    public Category DetermineCategory(JsonNode root) => Category.Cpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var memoryType = 
            root["specifications"]?["memory"]?["types"]?.AsArray()?.FirstOrDefault()?.ToString() ?? "Unknown";
        
        return new Dictionary<string, string>
        {
            { "Cores", root["cores"]?["total"]?.ToString() ?? "0" },
            { "Threads", root["cores"]?["threads"]?.ToString() ?? "0" },
            { "BaseClock", root["clocks"]?["performance"]?["base"]?.ToString() ?? "0" },
            { "BoostClock", root["clocks"]?["performance"]?["boost"]?.ToString() ?? "0" },
            { "TDP", root["specifications"]?["tdp"]?.ToString() ?? "0" },
            { "Socket", root["socket"]?.ToString() ?? "Unknown" },
            { "MemoryType", memoryType }
        };
    }
}