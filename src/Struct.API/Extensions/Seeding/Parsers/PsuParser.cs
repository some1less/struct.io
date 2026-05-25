using System.Text.Json.Nodes;
using Struct.DAL.Models;


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
            { "Wattage", ts?["Wattage"]?.ToString() ?? "0" },
            { "FormFactor", ts?["FormFactor"]?.ToString() ?? "Unknown" },
            { "Efficiency", ts?["Efficiency"]?.ToString() ?? "Unknown" },
            { "Modular", ts?["Modular"]?.ToString() ?? "Unknown" }
        };
    }
}