using System.Text.Json.Nodes;
using Struct.DAL.Models;

namespace Struct.API.Extensions.Seeding.Parsers;

public class StorageParser : IComponentParser
{
    public string TargetFolderName => "Storage";

    /* check storage_type = IMPORTANT */
    public Category DetermineCategory(JsonNode root)
    {
        var type = root["storage_type"]?.ToString() ?? root["type"]?.ToString();

        if (type == "HDD")
        {
            return Category.Hdd;
        }

        return Category.Ssd;
    }

    public Dictionary<string, string> ParseSpecs(JsonNode root)
    {
        return new Dictionary<string, string>
        {
            { "Capacity", root["capacity"]?.ToString() ?? "0" },
            { "FormFactor", root["form_factor"]?.ToString() ?? "Unknown" },
            { "Interface", root["interface"]?.ToString() ?? "Unknown" },
            { "Type", root["type"]?.ToString() ?? "Unknown" }
        };
    }
}