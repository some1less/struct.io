using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public class RamParser : IComponentParser
{
    public string TargetFolderName => "RAM";

    public Category DetermineCategory(JsonNode root) => Category.Ram;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        return new Dictionary<string, string>
        {
            { "Type", root["ram_type"]?.ToString() ?? "Unknown" },
            { "Capacity", root["capacity"]?.ToString() ?? "0" },
            { "Speed", root["speed"]?.ToString() ?? "0" },
            { "Modules", root["modules"]?["quantity"]?.ToString() ?? "1" }
        };
    }
}