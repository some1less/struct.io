using System.Text.Json.Nodes;
using Struct.DAL.Models;


namespace Struct.API.Extensions.Seeding.Parsers;

public class MotherboardParser : IComponentParser
{
    public string TargetFolderName => "Motherboard";

    public Category DetermineCategory(JsonNode root) => Category.Motherboard;

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { "Socket", ts?["Socket"]?.ToString() ?? "Unknown" },
            { "FormFactor", ts?["FormFactor"]?.ToString() ?? "Unknown" },
            { "RamType", ts?["RamType"]?.ToString() ?? "Unknown" },
            { "MaxRam", ts?["MaxRam"]?.ToString() ?? "0" },
            { "RamSlots", ts?["RamSlots"]?.ToString() ?? "0" }
        };
    }
}