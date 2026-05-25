using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public class GpuParser : IComponentParser
{
    public string TargetFolderName => "GPU";

    public Category DetermineCategory(JsonNode root) => Category.Gpu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { "VRAM", ts?["VRAM"]?.ToString() ?? "0" },
            { "CoreClock", ts?["CoreClock"]?.ToString() ?? "0" },
            { "Length", ts?["Length"]?.ToString() ?? "0" },
            { "TDP", ts?["TDP"]?.ToString() ?? "0" },
            { "Interface", ts?["Interface"]?.ToString() ?? "Unknown" }
        };
    }
}