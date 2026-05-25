using System.Text.Json.Nodes;
using Struct.DAL.Models;


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
            { "Type", ts?["Type"]?.ToString() ?? "Unknown" },
            { "Capacity", ts?["Capacity"]?.ToString() ?? "0" },
            { "Speed", ts?["Speed"]?.ToString() ?? "0" },
            { "Modules", ts?["Modules"]?.ToString() ?? "1" }
        };
    }
}