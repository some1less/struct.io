using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public class MotherboardParser : IComponentParser
{
    public string TargetFolderName => "Motherboard";

    public Category DetermineCategory(JsonNode root) => Category.Motherboard;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        return new Dictionary<string, string>
        {
            { "Socket", root["socket"]?.ToString() ?? "Unknown" },
            { "FormFactor", root["form_factor"]?.ToString() ?? "Unknown" },
            { "RamType", root["memory"]?["ram_type"]?.ToString() ?? "Unknown" },
            { "MaxRam", root["memory"]?["max"]?.ToString() ?? "0" },
            { "RamSlots", root["memory"]?["slots"]?.ToString() ?? "0" }
        };
    }
}