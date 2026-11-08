using System.Text.Json.Nodes;
using Struct.DAL.Models;
using Struct.BLL.Core.Specs;


namespace Struct.API.Extensions.Seeding.Parsers;

public class PsuParser : IComponentParser
{
    public string TargetFolderName => "PSU";

    public Category DetermineCategory(JsonNode root) => Category.Psu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { Spec.Wattage, ts?[Spec.Wattage]?.ToString() ?? "0" },
            { Spec.FormFactor, ts?[Spec.FormFactor]?.ToString() ?? "Unknown" },
            { Spec.Efficiency, ts?[Spec.Efficiency]?.ToString() ?? "Unknown" },
            { Spec.Modular, ts?[Spec.Modular]?.ToString() ?? "Unknown" }
        };
    }
}