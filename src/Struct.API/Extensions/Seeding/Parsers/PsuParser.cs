using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public class PsuParser : IComponentParser
{
    public string TargetFolderName => "PSU";

    public Category DetermineCategory(JsonNode root) => Category.Psu;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        return new Dictionary<string, string>
        {
            { "Wattage", root["wattage"]?.ToString() ?? "0" },
            { "FormFactor", root["form_factor"]?.ToString() ?? "Unknown" },
            { "Efficiency", root["efficiency_rating"]?.ToString() ?? "Unknown" },
            { "Modular", root["modular"]?.ToString() ?? "Unknown" }
        };
    }
}