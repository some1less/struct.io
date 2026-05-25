using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class StorageParser : IComponentParser
{
    public string TargetFolderName => "Storage";

    /* check storage_type = IMPORTANT */
    public Category DetermineCategory(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        var type = ts?["Type"]?.ToString();

        if (type == "HDD")
        {
            return Category.Hdd;
        }

        return Category.Ssd;
    }

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        var ts = root["TechnicalSpecs"];
        return new Dictionary<string, string>
        {
            { "Capacity", ts?["Capacity"]?.ToString() ?? "0" },
            { "FormFactor", ts?["FormFactor"]?.ToString() ?? "Unknown" },
            { "Interface", ts?["Interface"]?.ToString() ?? "Unknown" },
            { "Type", ts?["Type"]?.ToString() ?? "Unknown" }
        };
    }
}