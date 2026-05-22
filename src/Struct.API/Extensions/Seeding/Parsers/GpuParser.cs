using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public class GpuParser : IComponentParser
{
    public string TargetFolderName => "GPU";

    public Category DetermineCategory(JsonNode root) => Category.Gpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        return new Dictionary<string, string>
        {
            { "VRAM", root["memory"]?.ToString() ?? "0" },
            { "CoreClock", root["core_base_clock"]?.ToString() ?? "0" },
            { "Length", root["length"]?.ToString() ?? "0" },
            { "TDP", root["tdp"]?.ToString() ?? "0" },
            { "Interface", root["interface"]?.ToString() ?? "Unknown" }
        };
    }
}