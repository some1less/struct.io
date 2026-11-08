using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


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
            { Spec.Vram, ts?[Spec.Vram]?.ToString() ?? "0" },
            { Spec.CoreClock, ts?[Spec.CoreClock]?.ToString() ?? "0" },
            { Spec.Length, ts?[Spec.Length]?.ToString() ?? "0" },
            { Spec.Tdp, ts?[Spec.Tdp]?.ToString() ?? "0" },
            { Spec.Interface, ts?[Spec.Interface]?.ToString() ?? "Unknown" }
        };
    }
}