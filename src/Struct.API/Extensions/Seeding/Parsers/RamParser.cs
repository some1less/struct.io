using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class RamParser : IComponentParser
{
    public string TargetFolderName => "RAM";

    public Category DetermineCategory(JsonNode root) => Category.Ram;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { Spec.Type, ts?[Spec.Type]?.ToString() ?? "Unknown" },
            { Spec.Capacity, ts?[Spec.Capacity]?.ToString() ?? "0" },
            { Spec.Speed, ts?[Spec.Speed]?.ToString() ?? "0" },
            { Spec.Modules, ts?[Spec.Modules]?.ToString() ?? "1" }
        };
    }
}